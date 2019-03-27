import argparse, os, sys, shutil
from datetime import datetime, timedelta
from AzureUtil import AzureUtil
import Experimentation
import LogDownloader

if __name__ == '__main__':
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
    shutil.rmtree(output_dir) # Clean out logs directory
    output_gz_fp = LogDownloader.download_container(**vars(ld_args))
    
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