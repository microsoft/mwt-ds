import argparse, json, os, psutil, sys, shutil
from datetime import datetime
from subprocess import check_output, STDOUT
from AzureUtil import AzureUtil
import Experimentation
import FeatureImportance
import dashboard_utils
import LogDownloader
from loggers import Logger

import uuid

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
    try:
        # Change directory to working directory to have vw.exe in path
        os.chdir(os.path.dirname(os.path.realpath(__file__)))
        start_time = datetime.now()
        timestamp = start_time.strftime("%Y-%m-%d-%H_%M_%S")

        # Parse system parameters
        main_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
        main_parser.add_argument('--evaluation_id', help="evaluation id")
        main_parser.add_argument('--output_folder', help="storage account container's job folder where output files are stored", required=True)
        main_parser.add_argument('--dashboard_filename', help="name of the output dashboard file", default='aggregates.txt')
        main_parser.add_argument('--summary_json', help="json file containing custom policy commands to run", default='')
        main_parser.add_argument('--run_experimentation', help="run Experimentation.py", action='store_true')
        main_parser.add_argument('--delete_logs_dir', help="delete logs directory before starting to download new logs", action='store_true')
        main_parser.add_argument('--cleanup', help="delete logs and created files after use", action='store_true')
        main_parser.add_argument('--get_feature_importance', help="run FeatureImportance.py", action='store_true')
        main_parser.add_argument('--feature_importance_filename', help="name of the output feature importance file", default='featureimportance.json')
        main_parser.add_argument('--feature_importance_raw_filename', help="name of the output feature importance file with raw (unparsed) features", default='featureimportanceraw.json')
        main_parser.add_argument('--ml_args', help="the online policy that we need for calculating the feature importances", required=True)
        main_parser.add_argument('--geneva_namespace', help="namespace for Geneva logging")
        main_parser.add_argument('--geneva_host', help="host for Geneva logging")
        main_parser.add_argument('--geneva_port', help="port for Geneva logging", type=int)
        main_args, other_args = main_parser.parse_known_args(sys.argv[1:])
       
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

    
        # Download cooked logs
        output_gz_fp, total_download_size = LogDownloader.download_container(**vars(ld_args))

        if output_gz_fp == None:
            Logger.error('No logs found between start date: {0} and end date:{1}. Exiting ... '.format(ld_args.start_date, ld_args.end_date))
            sys.exit(1)

        #Init Azure Util
        azure_util = AzureUtil(ld_args.conn_string, ld_args.account_name, ld_args.sas_token)

        # Remove json files
        if main_args.cleanup:
            for f in os.listdir(output_dir):
                if f.endswith('json'):
                    Logger.info('Deleting ' + f)
                    os.remove(os.path.join(output_dir, f))

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
                        custom_command = "vw " + policyArgs + " -d " + output_gz_fp + " -p " + output_gz_fp + "." + policyName + ".pred"
                        try:
                            check_output(custom_command.split(' '), stderr=STDOUT)
                        except:
                            Logger.exception("Custom policy run failed")
            except:
                Logger.exception()

        if main_args.run_experimentation:
            experimentation_start_time = datetime.now()
            Logger.info('Running Experimentation')
            # Parse Experimentation args
            experimentation_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
            Experimentation.add_parser_args(experimentation_parser)
            other_args.append('-f')
            other_args.append(output_gz_fp)
            exp_args, other_args = experimentation_parser.parse_known_args(other_args)

            # Run Experimentation.py using output_gz_fp as input
            Experimentation.main(exp_args)
            experiments_file_path = os.path.join(os.getcwd(), "experiments.csv")
            azure_util.upload_to_blob(ld_args.app_id,  os.path.join(main_args.output_folder, "experiments.csv"), experiments_file_path)
            if main_args.cleanup: os.remove(experiments_file_path)

        # Generate dashboard files
        dashboard_file_path = os.path.join(output_dir, main_args.dashboard_filename)
        d = dashboard_utils.create_stats(output_gz_fp)
        dashboard_utils.output_dashboard_data(d, dashboard_file_path)
        azure_util.upload_to_blob(ld_args.app_id,  os.path.join(main_args.output_folder, main_args.dashboard_filename), dashboard_file_path)

        if main_args.get_feature_importance:
            feature_importance_start_time = datetime.now()
            Logger.info('Download model file')
            model_fp = None
            blobs = azure_util.list_blobs(ld_args.app_id)
            for blob in blobs:
                if '/model/' not in blob.name:
                    continue
                # blob.name looks like this: '20180416094500/model/2019/01/14.json'
                blob_day = datetime.strptime(blob.name.split('/model/', 1)[1].split('_', 1)[0].split('.', 1)[0], '%Y/%m/%d')
                if blob_day == ld_args.start_date:
                    model_fp = os.path.join(output_dir, 'model.vw')
                    azure_util.download_from_blob(ld_args.app_id, blob.name, model_fp)

            Logger.info('Generate Feature Importance')
            feature_importance_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
            FeatureImportance.add_parser_args(feature_importance_parser)
            other_args.append('--data')
            other_args.append(output_gz_fp)

            if model_fp:
                other_args.append('--model')
                other_args.append(model_fp)
            other_args.append('--min_num_features')
            other_args.append('1')
            other_args.append('--ml_args')
            other_args.append(main_args.ml_args)
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

        # Merge calculated policies into summary file path, upload summary file
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
                                        'arguments' :p['arguments']
                                    })
                    except:
                        Logger.exception()
                with open(summary_file_path, 'w') as outfile:
                    json.dump(summary_data, outfile)
                azure_util.upload_to_blob(ld_args.app_id, os.path.join(main_args.output_folder, main_args.summary_json), summary_file_path)
        Logger.info("Done executing job")
    except:
        Logger.exception('Job failed.')
        sys.exit(1)
    finally:
        if main_args.cleanup:
            Logger.info('Deleting folder as part of cleanup: {}'.format(ld_args.log_dir))
            shutil.rmtree(ld_args.log_dir, ignore_errors=True)

        end_time = datetime.now()
        Logger.info('Total Job time in seconds: {}'.format((end_time - start_time).seconds))
        sys.stdout.flush()
        sys.stderr.flush()
        azure_util.upload_to_blob(ld_args.app_id, os.path.join(main_args.output_folder, 'stdout.txt'), os.path.join(task_dir, 'stdout.txt'))
        azure_util.upload_to_blob(ld_args.app_id, os.path.join(main_args.output_folder, 'stderr.txt'), os.path.join(task_dir, 'stderr.txt'))
        Logger.close()