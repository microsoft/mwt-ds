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
    start_time = datetime.now()
    timestamp = start_time.strftime("%Y-%m-%d-%H_%M_%S")

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

    parser.add_argument("--output_connection_string", type=str, help="output connection_string")
    parser.add_argument("--output_container", type=str, help="output_container")
    parser.add_argument('--output_path', help="name of the output dashboard file", default='aggregates.txt')

    parser.add_argument('--summary_json', help="json file containing custom policy commands to run", default='')

    parser.add_argument('--get_feature_importance', help="run FeatureImportance.py", action='store_true')
    parser.add_argument('--feature_importance_filename', help="name of the output feature importance file", default='featureimportance.json')
    parser.add_argument('--feature_importance_raw_filename', help="name of the output feature importance file with raw (unparsed) features", default='featureimportanceraw.json')
    parser.add_argument('--ml_args', help="the online policy that we need for calculating the feature importances", required=True)

    parser.add_argument('--log_type', help="cooked log format e.g. cb, ccb", default='cb')

    args = parser.parse_args()

    telemetry_client = get_telemetry_client(args.appInsightsInstrumentationKey)

    tmp_folder = args.tmp_folder
    os.makedirs(tmp_folder, exist_ok=True)
    dashboard_e2e.dashboard_e2e(args)
