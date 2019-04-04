import argparse, os, psutil, sys, shutil, json
from datetime import datetime, timedelta
from AzureUtil import AzureUtil
from Experimentation import Command
import Experimentation
import dashboard_utils
import LogDownloader

def check_system():
    try:
        print('Cpu count : {}'.format(psutil.cpu_count()))
        print('Cpu count : {}'.format(psutil.cpu_count(logical=False)))
        for disk in psutil.disk_partitions():
            print(disk.device)
            bytes_in_gb = 1024**3
            print('Total size: {:.3f} GB'.format(psutil.disk_usage(disk.device).total / bytes_in_gb))
            print('Used size: {:.3f} GB'.format(psutil.disk_usage(disk.device).used / bytes_in_gb))
            print('Free size: {:.3f} GB'.format(psutil.disk_usage(disk.device).free / bytes_in_gb))
    except Exception as e:
            print(e)

if __name__ == '__main__':
    check_system()
    # Change directory to working directory to have vw.exe in path
    os.chdir(os.path.dirname(os.path.realpath(__file__)))
    t1 = datetime.now()
    timestamp = t1.strftime("%Y-%m-%d-%H_%M_%S")
    
    # Parse system parameters
    main_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
    main_parser.add_argument('--output_folder', help="storage account container's job folder where output files are stored", required=True)
    main_parser.add_argument('--dashboard_filename', help="name of the output dashboard file", default='aggregates5m.txt')
    main_parser.add_argument('--summary_json', help="json file containing custom policy commands to run", default='')
    main_parser.add_argument('--run_experimentation', help="run Experimentation.py", action='store_true')
    main_parser.add_argument('--delete_logs_dir', help="delete logs directory before starting to download new logs", action='store_true')
    main_parser.add_argument('--cleanup', help="delete created files after use", action='store_true')
    main_args, unknown = main_parser.parse_known_args(sys.argv[1:])
    
    # Parse LogDownloader args
    logdownloader_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
    LogDownloader.add_parser_args(logdownloader_parser)
    ld_args, unknown = logdownloader_parser.parse_known_args(unknown)
    output_dir = ld_args.log_dir +"\\" + ld_args.app_id

     # Clean out logs directory
    if main_args.delete_logs_dir and os.path.isdir(ld_args.log_dir):
        print('Deleting ' + ld_args.log_dir)
        shutil.rmtree(ld_args.log_dir, ignore_errors=True)

    # Download cooked logs
    output_gz_fp = LogDownloader.download_container(**vars(ld_args))

    #Init Azure Util
    azure_util = AzureUtil(ld_args.conn_string)

    # Remove json files
    if main_args.cleanup:
        for f in os.listdir(output_dir):
            if f.endswith('json'):
                print('Deleting ' + f)
                os.remove(os.path.join(output_dir, f))

    # Evaluate custom policies
    if main_args.summary_json:
        print('Evaluating custom policies')
        summary_file_path = os.path.join(output_dir, main_args.summary_json)
        azure_util.download_from_blob(ld_args.app_id,  main_args.output_folder + "\\"+ main_args.summary_json, summary_file_path)
        try:
            with open(summary_file_path) as summary_file:
                data = json.load(summary_file)
                for p in data['PolicyResults']:
                    print('Name: ' + p['Name'])
                    print('Command: ' + p['Arguments'])
                    custom_command = Command("vw " + p['Arguments'] + " -d " + output_gz_fp + " -p " + output_gz_fp + "." + p['Name'] + ".pred")
                    Experimentation.run_experiment(custom_command)
        except Exception as e:
            print(e)

    if main_args.run_experimentation:
        print('Running Experimentation')
        # Parse Experimentation args
        experimentation_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
        Experimentation.add_parser_args(experimentation_parser)
        unknown.append('-f')
        unknown.append(output_gz_fp)
        exp_args, exp_unknown = experimentation_parser.parse_known_args(unknown)
    
        # Run Experimentation.py using output_gz_fp as input
        Experimentation.main(exp_args)
        experiments_file_path = os.path.join(os.getcwd(), "experiments.csv")
        azure_util.upload_to_blob(ld_args.app_id,  main_args.output_folder + "\\"+ "experiments.csv", experiments_file_path)
        if main_args.cleanup: os.remove(experiments_file_path)

    # Generate dashboard files
    dashboard_file_path = os.path.join(output_dir, main_args.dashboard_filename)
    dashboard_utils.create_stats(output_gz_fp, dashboard_file_path)
    azure_util.upload_to_blob(ld_args.app_id,  main_args.output_folder + "\\"+ main_args.dashboard_filename, dashboard_file_path)

    # Merge calculated policies into summary file path, upload summary file
    if main_args.summary_json:
        summary_file_path = os.path.join(output_dir, main_args.summary_json)
        if os.path.isfile(summary_file_path):
            with open(summary_file_path) as summary_file:
                summary_data = json.load(summary_file)
                summary_data['Status'] = 0 # Success status
                try:
                    policy_file_path = os.path.join(output_dir, "policy.json")
                    if os.path.isfile(policy_file_path):
                        with open(policy_file_path) as policy_file:
                            policy_data = json.load(policy_file)
                            for p in policy_data['Policies']:
                                summary_data['PolicyResults'].append({
                                    'Name':p['Name'],
                                    'Arguments':p['Arguments']
                                })
                except Exception as e:
                    print(e)
            with open(summary_file_path, 'w') as outfile:
                json.dump(summary_data, outfile)
            azure_util.upload_to_blob(ld_args.app_id,  main_args.output_folder + "\\"+ main_args.summary_json, summary_file_path)

    if main_args.cleanup:
        for f in os.listdir(output_dir):
            os.remove(os.path.join(output_dir, f))
            
    t2 = datetime.now()
    print("Done executing job")
    print('Total Job time:',(t2-t1)-timedelta(microseconds=(t2-t1).microseconds))