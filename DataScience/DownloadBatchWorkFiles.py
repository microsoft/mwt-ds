from AzureUtil import AzureUtil
import argparse
import os, sys

if __name__ == '__main__':
    parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
    parser.add_argument('--system_conn_string', help="storage account connection string where source/output files are stored", required=True)
    parser.add_argument('--system_files_container', help="storage account container where source files are stored", required=True)
    parser.add_argument('--local_working_dir', help="local dir where source files are stored", required=True)
    main_args = parser.parse_args(sys.argv[1:])
    if not os.path.exists(main_args.local_working_dir):
        os.makedirs(main_args.local_working_dir)
    azure_util = AzureUtil(main_args.system_conn_string)
    azure_util.download_all_blobs(main_args.system_files_container, main_args.local_working_dir)