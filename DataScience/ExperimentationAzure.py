import argparse, os, psutil, sys, shutil
from datetime import datetime, timedelta
from AzureUtil import AzureUtil
import Experimentation
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
    main_parser.add_argument('--system_conn_string', help="storage account connection string where source/output files are stored", required=True)
    main_parser.add_argument('--system_output_container', help="storage account container where output files are stored", required=True)
    main_args, unknown = main_parser.parse_known_args(sys.argv[1:])
    
    # Download cooked logs
    logdownloader_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
    LogDownloader.add_parser_args(logdownloader_parser)
    ld_args, unknown = logdownloader_parser.parse_known_args(unknown)
    output_dir = ld_args.log_dir +"\\" + ld_args.app_id
    if os.path.isdir(ld_args.log_dir):
        print('Deleting ' + ld_args.log_dir)
        shutil.rmtree(ld_args.log_dir) # Clean out logs directory
    output_gz_fp = LogDownloader.download_container(**vars(ld_args))
    for f in os.listdir(output_dir):
        if f.endswith('json'):
            print('Deleting ' + f)
            os.remove(os.path.join(output_dir, f))

    # Run Experimentation.py using output_gz_fp as input
    experimentation_parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
    Experimentation.add_parser_args(experimentation_parser)
    unknown.append('-f')
    unknown.append(output_gz_fp)
    exp_args, exp_unknown = experimentation_parser.parse_known_args(unknown)
    Experimentation.main(exp_args)
    
    # Upload prediction files and experiments.csv, cleanup
    azure_util = AzureUtil(main_args.system_conn_string)
    azure_util.upload_to_blob(main_args.system_output_container,  ld_args.app_id + "\\"+ timestamp + "\\experiments.csv", os.path.join(os.getcwd(), "experiments.csv"))
    for f in os.listdir(output_dir):
        file_path = os.path.join(output_dir, f)
        if f.startswith(os.path.basename(output_gz_fp)) and f.endswith('pred'):
            azure_util.upload_to_blob(main_args.system_output_container,  ld_args.app_id + "\\" + timestamp + "\\" + f, file_path)
        os.remove(file_path)
            
    t2 = datetime.now()
    print("Done executing job")
    print('Upload Time:',(t2-t1)-timedelta(microseconds=(t2-t1).microseconds))