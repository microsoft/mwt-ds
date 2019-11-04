import argparse
import datetime
import os
from shutil import rmtree
from azure.storage.blob import BlockBlobService
from helpers import vw, preprocessing, grid, sweep, command, dashboard
from helpers.environment import Environment
from helpers.constant import LOG_CHUNK_SIZE
from helpers.input_provider import AzureLogsProvider


def dashboard_e2e(app_container, connection_string, app_folder, vw_path, start, end, tmp_folder, runtime_mode, procs, log_level, output_connection_string, output_container, output_path, enable_sweep, delta_mod_t=3600, max_connections=50):
    env = Environment(vw_path, runtime_mode, procs, log_level, tmp_folder)

    commands = {}

    bbs = BlockBlobService(connection_string=connection_string)
    base_command = {'#base': '--cb_adf --dsjson --compressed --save_resume --preserve_performance_counters'}

    namespaces = set()
    marginals_grid = []
    interactions_grid = []

    for blob_index, blob in enumerate(AzureLogsProvider.iterate_blobs(bbs, app_container, app_folder, start, end)):
        blob_properties = bbs.get_blob_properties(app_container, blob.name).properties

        current_time = datetime.datetime.now(datetime.timezone.utc)
        if  current_time - blob_properties.last_modified < datetime.timedelta(0, delta_mod_t):
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
                vw.cache(base_command, env, local_log_path)

                if (blob_index == 0 and index == 0):
                    namespaces = preprocessing.extract_namespaces(
                        open(local_log_path, 'r', encoding='utf-8')
                    )

                    marginals_grid = preprocessing.get_marginals_grid(
                        '#marginals', namespaces[2]
                    )

                    interactions_grid = preprocessing.get_interactions_grid(
                        '#interactions', namespaces[0], namespaces[1]
                    )

                    env.logger.info("namespaces: " + str(namespaces))

            index += 1

            env.local_logs_provider.get_metadata(local_log_path)
    if enable_sweep:
        multi_grid = grid.generate(interactions_grid, marginals_grid)
        best = sweep.sweep(multi_grid, env, base_command)

        predict_opts = {'#base': '--cb_explore_adf --epsilon 0.2 --compressed --dsjson --save_resume --preserve_performance_counters'}
        commands = dict(map(
            lambda lo: (lo[0], command.apply(lo[1], predict_opts)),
            best.items()
        ))
        vw.predict(commands, env)

    local_dashboard_path = os.path.join(tmp_folder, 'dashboard.json')
    dashboard.create(local_dashboard_path, env, commands, enable_sweep)
    if env.runtime.is_master() and output_connection_string:
        bbs = BlockBlobService(connection_string=output_connection_string)
        env.logger.info(output_container + ':' + output_path + ': Uploading from ' + local_dashboard_path)
        bbs.create_blob_from_path(output_container, output_path, local_dashboard_path, max_connections=4)
        env.logger.info(output_container + ':' + output_path + ': Succeesfully uploaded')
    rmtree(tmp_folder)


def main():
    parser = argparse.ArgumentParser("dashboard e2e")
    parser.add_argument("--app_folder", type=str, help="app folder")
    parser.add_argument("--vw", type=str, help="vw path")
    parser.add_argument("--start_date", type=str, help="start date")
    parser.add_argument("--end_date", type=str, help="end date")
    parser.add_argument("--enable_sweep", action='store_true')
    parser.add_argument("--tmp_folder", type=str, help="temporary folder")
    parser.add_argument("--app_container", type=str, help="app_container")
    parser.add_argument("--connection_string", type=str, help="connection_string")
    parser.add_argument("--procs", type=int, help="procs")
    parser.add_argument("--env", type=str, help="environment (local / mpi)", default="local")
    parser.add_argument("--log_level", type=str, help="log level (CRITICAL / ERROR / WARNING / INFO / DEBUG)", default='INFO')
    parser.add_argument("--output_connection_string", type=str, help="output connection_string")
    parser.add_argument("--output_container", type=str, help="output_container")
    parser.add_argument("--output_path", type=str, help="dashboard file's path inside output container")

    args = parser.parse_args()

    date_format = '%m/%d/%Y'

    os.makedirs(args.tmp_folder, exist_ok=True)

    start = datetime.datetime.strptime(args.start_date, date_format)
    end = datetime.datetime.strptime(args.end_date, date_format)

    dashboard_e2e(args.app_container, args.connection_string, args.app_folder,
                  args.vw, start, end, args.tmp_folder, args.env, args.procs,
                  args.log_level, args.output_connection_string,
                  args.output_container, args.output_path, args.enable_sweep)


if __name__ == '__main__':
    main()
