import argparse, json, os, psutil, sys, shutil
from datetime import datetime
from subprocess import check_output, STDOUT
import uuid
from applicationinsights import TelemetryClient
from DashboardMpi import dashboard_e2e

def get_telemetry_client(appInsightsInstrumentationKey):
    if appInsightsInstrumentationKey:
        client = TelemetryClient(appInsightsInstrumentationKey)
        client.context.operation.id = str(uuid.uuid4())
        return client
    else:
        return None

def check_system():
    try:
        bytes_in_gb = 1024**3
        Logger.info('Cpu count : {}\n'.format(psutil.cpu_count()) +
                    'Cpu count : {}\n'.format(psutil.cpu_count(logical=False)) +
                    '/mnt Total size: {:.3f} GB\n'.format(shutil.disk_usage('/mnt').total / bytes_in_gb) +
                    '/mnt Used size:  {:.3f} GB\n'.format(shutil.disk_usage('/mnt').used  / bytes_in_gb) +
                    '/mnt Free size:  {:.3f} GB\n'.format(shutil.disk_usage('/mnt').free  / bytes_in_gb))
    except:
        Logger.exception()

if __name__ == '__main__':
    # Parse system parameters
    main_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
    main_parser.add_argument('--evaluation_id', help="evaluation id")
    main_parser.add_argument('--output_folder', help="storage account container's job folder where output files are stored", required=True)
    main_parser.add_argument('--dashboard_filename', help="name of the output dashboard file", default='aggregates.txt')
    main_parser.add_argument('--total_aggregates_filename', help="name of the output file for total aggregates", default='totalaggregates.json')
    main_parser.add_argument('--summary_json', help="json file containing custom policy commands to run", default='')
    main_parser.add_argument('--run_experimentation', help="run Experimentation.py", action='store_true')
    main_parser.add_argument('--delete_logs_dir', help="delete logs directory before starting to download new logs", action='store_true')
    main_parser.add_argument('--cleanup', help="delete logs and created files after use", action='store_true')
    main_parser.add_argument('--get_feature_importance', help="run FeatureImportance.py", action='store_true')
    main_parser.add_argument('--feature_importance_filename', help="name of the output feature importance file", default='featureimportance.json')
    main_parser.add_argument('--feature_importance_raw_filename', help="name of the output feature importance file with raw (unparsed) features", default='featureimportanceraw.json')
    main_parser.add_argument('--feature_importance_timeout', help="timeout for computing the feature importance (default: 5 hours)", type=int, default=5*3600)
    main_parser.add_argument('--ml_args', help="the online policy that we need for calculating the feature importances", required=True)
    main_parser.add_argument('--geneva_namespace', help="namespace for Geneva logging")
    main_parser.add_argument('--geneva_host', help="host for Geneva logging")
    main_parser.add_argument('--geneva_port', help="port for Geneva logging", type=int)
    main_parser.add_argument('--log_type', help="cooked log format e.g. cb, ccb", default='cb')
    main_args, other_args = main_parser.parse_known_args(sys.argv[1:])

    try:
        # Change directory to working directory to have vw.exe in path
        os.chdir(os.path.dirname(os.path.realpath(__file__)))
        start_time = datetime.now()
        timestamp = start_time.strftime("%Y-%m-%d-%H_%M_%S")

        # Parse LogDownloader args
        log_download_start_time = datetime.now()
        logdownloader_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
        LogDownloader.add_parser_args(logdownloader_parser)
        other_args.append('-o')
        other_args.append('2')
        other_args.append('--report_progress')
        other_args.append('false')
        ld_args, other_args = logdownloader_parser.parse_known_args(other_args)
        output_dir = os.path.join(ld_args.log_dir, ld_args.app_id)
        task_dir = os.path.dirname(os.path.dirname(ld_args.log_dir))

        geneva_gbl_vals = {'appId': ld_args.app_id, 'jobId': main_args.evaluation_id}

        Logger.create_loggers(geneva_namespace=main_args.geneva_namespace,
                              geneva_host=main_args.geneva_host,
                              geneva_port=main_args.geneva_port,
                              geneva_gbl_vals=geneva_gbl_vals)

        check_system()

        # Clean out logs directory
        if main_args.delete_logs_dir and os.path.isdir(ld_args.log_dir):
            Logger.info('Deleting ' + ld_args.log_dir)
            shutil.rmtree(ld_args.log_dir, ignore_errors=True)

    # Parse system parameters
    parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)

    parser.add_argument('--appInsightsInstrumentationKey', help="App Insights key for logging metrics")
    parser.add_argument('--evaluation_id', help="evaluation id")
    parser.add_argument('--tmp_folder', help="storage account container's job folder where output files are stored", required=True)
    parser.add_argument('--delete_logs_dir', help="delete logs directory before starting to download new logs", action='store_true')
    parser.add_argument('--cleanup', help="delete logs and created files after use", action='store_true')
    parser.add_argument("--env", type=str, help="environment (local / mpi)", default="local")

    parser.add_argument("--connection_string", type=str, help="connection_string")
    parser.add_argument("--account_name", type=str, help="account name from sas URI")
    parser.add_argument("--sas_token", type=str, help="sas token")
    parser.add_argument("--app_container", type=str, help="app_container")
    parser.add_argument("--start_date", type=str, help="start date")
    parser.add_argument("--end_date", type=str, help="end date")
    parser.add_argument("--log_level", type=str, help="log level (CRITICAL / ERROR / WARNING / INFO / DEBUG)", default='INFO')

    parser.add_argument('--enable_sweep', help="run Experimentation.py", action='store_true')
    parser.add_argument("--procs", type=int, help="procs")

        # Evaluate custom policies
        if main_args.summary_json:
            Logger.info('Evaluating custom policies')
            summary_file_path = os.path.join(output_dir, main_args.summary_json)
            azure_util.download_from_blob(ld_args.app_id, os.path.join(main_args.output_folder, main_args.summary_json), summary_file_path)
            try:
                with open(summary_file_path) as summary_file:
                    data = json.load(summary_file)
                    for p in data['policyResults']:
                        policyName = p['name']
                        policyArgs = p['arguments']
                        Logger.info('Name: ' + policyName)
                        Logger.info('Command: ' + policyArgs)
                        custom_command = "vw " + policyArgs + " -d " + output_gz_fp + " -p " + output_gz_fp + "." + policyName + ".pred" + " -f " + os.path.join(output_dir, 'model.' + policyName + '.vw')
                        try:
                            check_output(custom_command.split(' '), stderr=STDOUT)
                        except:
                            Logger.exception("Custom policy run failed")
            except:
                Logger.exception()

    parser.add_argument('--summary_json', help="json file containing custom policy commands to run", default='')

    parser.add_argument('--get_feature_importance', help="run FeatureImportance.py", action='store_true')
    parser.add_argument('--feature_importance_filename', help="name of the output feature importance file", default='featureimportance.json')
    parser.add_argument('--feature_importance_raw_filename', help="name of the output feature importance file with raw (unparsed) features", default='featureimportanceraw.json')
    parser.add_argument('--ml_args', help="the online policy that we need for calculating the feature importances", required=True)

    parser.add_argument('--log_type', help="cooked log format e.g. cb, ccb", default='cb')

        # Generate dashboard and model files
        dashboard_file_path = os.path.join(output_dir, main_args.dashboard_filename)
        d = dashboard_utils.create_stats(output_gz_fp, main_args.log_type)
        total_aggregates = dashboard_utils.output_dashboard_data(d, dashboard_file_path)
        azure_util.upload_to_blob(ld_args.app_id,  os.path.join(main_args.output_folder, main_args.dashboard_filename), dashboard_file_path)
        total_aggregates_file_path = os.path.join(output_dir, main_args.total_aggregates_filename)
        with open(total_aggregates_file_path, 'w') as f:
            f.write(json.dumps({"d":total_aggregates}))
        azure_util.upload_to_blob(ld_args.app_id,  os.path.join(main_args.output_folder, main_args.total_aggregates_filename), total_aggregates_file_path)
        for modelfile in os.listdir(output_dir):
            if modelfile.endswith(".vw"):
                azure_util.upload_to_blob(ld_args.app_id, os.path.join(main_args.output_folder, modelfile),  os.path.join(output_dir, modelfile))

        if main_args.get_feature_importance:
            feature_importance_start_time = datetime.now()
            Logger.info('Download model file')
            online_model_fp = None
            blobs = azure_util.list_blobs(ld_args.app_id)
            for blob in blobs:
                if '/model/' not in blob.name:
                    continue
                # blob.name looks like this: '20180416094500/model/2019/01/14.json'
                blob_day = datetime.strptime(blob.name.split('/model/', 1)[1].split('_', 1)[0].split('.', 1)[0], '%Y/%m/%d')
                if blob_day == ld_args.start_date:
                    online_model_fp = os.path.join(output_dir, 'model.online.vw')
                    azure_util.download_from_blob(ld_args.app_id, blob.name, online_model_fp)

            Logger.info('Generate Feature Importance')
            feature_importance_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
            FeatureImportance.add_parser_args(feature_importance_parser)
            other_args.append('--data')
            other_args.append(output_gz_fp)

            if online_model_fp:
                other_args.append('--model')
                other_args.append(online_model_fp)
            other_args.append('--min_num_features')
            other_args.append('1')

            # Add a timeout for computing invert hash so that FeatureImportance does not take forever to run.
            other_args.append('--invert_hash_timeout')
            other_args.append(str(main_args.feature_importance_timeout))

            # Temporary ccb defaults until passing user provided ml args issue is resolved
            if main_args.log_type == 'ccb':
                other_args.append('--ml_args')
                other_args.append("--ccb_explore_adf --epsilon 0 -l 0.01")

            fi_args, other_args = feature_importance_parser.parse_known_args(other_args)

            # Run FeatureImportance.py using output_gz_fp as input
            feature_buckets, pretty_feature_buckets = FeatureImportance.main(fi_args)

            # Feature importance values that are user-friendly strings
            feature_importance_file_path = os.path.join(output_dir, main_args.feature_importance_filename)
            with open(feature_importance_file_path, 'w') as feature_importance_file:
                json.dump(pretty_feature_buckets, feature_importance_file)
            azure_util.upload_to_blob(ld_args.app_id, os.path.join(main_args.output_folder, main_args.feature_importance_filename), feature_importance_file_path)

            # Feature importance values that are hashes returned by vw
            feature_importance_raw_file_path = os.path.join(output_dir, main_args.feature_importance_raw_filename)
            with open(feature_importance_raw_file_path, 'w') as feature_importance_raw_file:
                json.dump(feature_buckets, feature_importance_raw_file)
            azure_util.upload_to_blob(ld_args.app_id, os.path.join(main_args.output_folder, main_args.feature_importance_raw_filename), feature_importance_raw_file_path)

        # Merge calculated policies into summary file path, upload summary file and model files
        if main_args.summary_json:
            summary_file_path = os.path.join(output_dir, main_args.summary_json)
            if os.path.isfile(summary_file_path):
                with open(summary_file_path) as summary_file:
                    summary_data = json.load(summary_file)
                    summary_data['status'] = 0 # Success status
                    try:
                        policy_file_path = os.path.join(output_dir, "policy.json")
                        if os.path.isfile(policy_file_path):
                            with open(policy_file_path) as policy_file:
                                policy_data = json.load(policy_file)
                                for p in policy_data['policies']:
                                    summary_data['policyResults'].append({
                                        'name': p['name'],
                                        'arguments': p['arguments'],
                                        'policySource': 'OfflineExperimentation'
                                    })
                    except:
                        Logger.exception()
                with open(summary_file_path, 'w') as outfile:
                    json.dump(summary_data, outfile)
                azure_util.upload_to_blob(ld_args.app_id, os.path.join(main_args.output_folder, main_args.summary_json), summary_file_path)

                # upload model files if present.
                for filename in os.listdir(output_dir):
                    if os.path.isfile(os.path.join(output_dir, filename)) and filename.endswith(".vw"):
                        azure_util.upload_to_blob(ld_args.app_id, os.path.join(main_args.output_folder, filename), os.path.join(output_dir, filename))
        Logger.info("Done executing job")
    except:
        Logger.exception('Job failed.')
        sys.exit(1)
    finally:
        if main_args.cleanup:
            Logger.info('Deleting folder as part of cleanup: {}'.format(ld_args.log_dir))
            shutil.rmtree(ld_args.log_dir, ignore_errors=True)

    tmp_folder = args.tmp_folder
    os.makedirs(tmp_folder, exist_ok=True)
    dashboard_e2e.dashboard_e2e(args)
