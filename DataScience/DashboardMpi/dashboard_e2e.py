import argparse
import datetime
import os
import json
from subprocess import check_output, STDOUT
from shutil import rmtree
from azure.storage.blob import BlockBlobService
from DashboardMpi.helpers import vw, preprocessing, grid, sweep, command, dashboard
from DashboardMpi.helpers.environment import Environment
from DashboardMpi.helpers.constant import LOG_CHUNK_SIZE
from DashboardMpi.helpers.input_provider import AzureLogsProvider


def dashboard_e2e(args, delta_mod_t=3600, max_connections=50):
    connection_string = args.connection_string
    account_name = args.account_name
    sas_token = args.sas_token
    app_container = args.app_container
    tmp_folder = args.tmp_folder
    runtime_mode = args.env
    procs = args.procs
    log_level = args.log_level
    output_connection_string = args.output_connection_string
    output_container = args.output_container
    output_path = args.output_path
    enable_sweep = args.enable_sweep
    summary_json = args.summary_json

    log_type = args.log_type

    date_format = '%m/%d/%Y'
    start = datetime.datetime.strptime(args.start_date, date_format)
    end = datetime.datetime.strptime(args.end_date, date_format)

    env = Environment(runtime_mode, procs, log_level, tmp_folder)

    commands = {}

    if connection_string:
        bbs = BlockBlobService(connection_string=connection_string)
    elif account_name and sas_token:
        bbs = BlockBlobService(account_name=account_name, sas_token=sas_token)

    base_command = {'#base': '--cb_adf --dsjson --compressed --save_resume --preserve_performance_counters'}

    if log_type == 'ccb':
        base_command = {'#base': '--ccb_explore_adf --epsilon 0 --dsjson --compressed --save_resume --preserve_performance_counters'}

    namespaces = set()
    marginals_grid = []
    interactions_grid = []

    for blob_index, blob in enumerate(AzureLogsProvider.iterate_blobs(bbs, app_container, start, end)):
        blob_properties = bbs.get_blob_properties(app_container, blob.name).properties

        current_time = datetime.datetime.now(datetime.timezone.utc)
        if current_time - blob_properties.last_modified < datetime.timedelta(0, delta_mod_t):
            max_connections = 1

        start_range = 0
        index = 0
        while (start_range < blob_properties.content_length):
            local_log_path = env.local_logs_provider.new_path(blob.name, index)

            env.logger.info(blob.name + ': Downloading to ' + local_log_path)

            end_range = start_range + LOG_CHUNK_SIZE
            AzureLogsProvider.download_blob(
                bbs,
                app_container,
                blob.name,
                local_log_path,
                start_range,
                end_range,
                max_connections
            )

            last_line_length = AzureLogsProvider.truncate_log(local_log_path)
            start_range = end_range + 1 - last_line_length

            env.logger.info(local_log_path + ': Done.')

            if enable_sweep:
                if (blob_index == 0 and index == 0):
                    vw.check_vw_installed(env.logger)

                    namespaces = preprocessing.extract_namespaces(
                        open(local_log_path, 'r', encoding='utf-8'),
                        log_type
                    )

                    marginals_grid = preprocessing.get_marginals_grid(
                        '#marginals', namespaces[2]
                    )

                    interactions_grid = preprocessing.get_interactions_grid(
                        '#interactions', namespaces[0], namespaces[1]
                    )

                    env.logger.info("namespaces: " + str(namespaces))
                vw.cache(base_command, env, local_log_path)

            index += 1

            if log_type == 'cb':
                env.local_logs_provider.get_metadata(local_log_path)

    # Evaluate custom policies
    if summary_json:
        env.logger.info('Evaluating custom policies')
        local_summary_file_path = os.path.join(tmp_folder, summary_json)
        output_bbs = BlockBlobService(connection_string=output_connection_string)

        AzureLogsProvider.download_blob(
            output_bbs,
            os.path.join(output_container, summary_json),
            local_summary_file_path
        )

        try:
            with open(local_summary_file_path) as summary_file:
                data = json.load(summary_file)
                for p in data['policyResults']:
                    policy_name = p['name']
                    policy_args = p['arguments']
                    if '--save_resume' not in policy_args:
                        policy_args += ' --save_resume --preserve_performance_counters'

                    env.logger.info('Name: ' + policy_name)
                    env.logger.info('Command: ' + policy_args)

                    try:
                        custom_command = {}
                        custom_command[policy_name] = {
                            '#base': policy_args
                        }
                        vw.predict(custom_command, env)

                    except Exception as e:
                        env.logger.error("Custom policy run failed")
                        env.logger.error(e)

        except Exception as e:
            env.logger.error(e)

    if enable_sweep:
        multi_grid = grid.generate(interactions_grid, marginals_grid)
        best = sweep.sweep(multi_grid, env, base_command)

        predict_opts = {'#base': '--cb_explore_adf --epsilon 0.2 --compressed --dsjson --save_resume --preserve_performance_counters'}

        if log_type == 'ccb':
            predict_opts = {'#base': '--ccb_explore_adf --epsilon 0.2 --compressed --dsjson --save_resume --preserve_performance_counters'}

        commands = dict(map(
            lambda lo: (lo[0], command.apply(lo[1], predict_opts)),
            best.items()
        ))
        vw.predict(commands, env)

    local_dashboard_path = os.path.join(tmp_folder, 'dashboard.json')
    dashboard.create(local_dashboard_path, env, commands, enable_sweep, log_type)
    if env.runtime.is_master() and output_connection_string:
        bbs = BlockBlobService(connection_string=output_connection_string)
        env.logger.info(output_container + ':' + output_path + ': Uploading from ' + local_dashboard_path)
        bbs.create_blob_from_path(output_container, output_path, local_dashboard_path, max_connections=4)
        env.logger.info(output_container + ':' + output_path + ': Succeesfully uploaded')

    # Clean out logs directory
    if args.delete_logs_dir and os.path.isdir(tmp_folder):
        logs_dir = os.path.join(tmp_folder, 'logs')
        env.logger.info('Deleting ' + logs_dir)
        shutil.rmtree(logs_dir, ignore_errors=True)

    # Remove json files
    if args.cleanup and os.path.isdir(tmp_folder):
        env.logger.info('Deleting ' + tmp_folder)
        shutil.rmtree(tmp_folder, ignore_errors=True)
