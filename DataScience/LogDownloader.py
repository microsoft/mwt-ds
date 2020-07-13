import sys
from loggers import Logger

if sys.maxsize < 2**32:     # check for 32-bit python version
    if input("32-bit python interpreter detected. There may be problems downloading large files. Do you want to continue anyway [Y/n]? ") not in {'Y', 'y'}:
        sys.exit()

import os, time, datetime, argparse, gzip, shutil, ds_parse
try:
    from azure.storage.blob import BlockBlobService
except ImportError as e:
    if input("azure.storage.blob is required. Do you want to install it [Y/n]? ") in {'Y', 'y'}:
        import pip
        pip.main(['install', 'azure.storage.blob'])
        Logger.info('Please re-run script.')
    Logger.exception()
    sys.exit(1)


def valid_date(s):
    try:
        return datetime.datetime.strptime(s, "%Y-%m-%d")
    except ValueError:
        raise argparse.ArgumentTypeError("Not a valid date: '{0}'. Expected format: YYYY-MM-DD".format(s))
        
def cmp_files(f1, f2, start_range_f1=0, start_range_f2=0, erase_checkpoint_line=True):
    with open(f1, 'rb+' if erase_checkpoint_line else 'rb') as fp1, open(f2, 'rb') as fp2:
        if start_range_f1 != 0:
            fp1.seek(start_range_f1, os.SEEK_SET if start_range_f1 > 0 else os.SEEK_END)
        if start_range_f2 != 0:
            fp2.seek(start_range_f2, os.SEEK_SET if start_range_f2 > 0 else os.SEEK_END)
            
        prev_b1 = b''
        while True:
            b1 = fp1.read(1)
            if b1 != fp2.read(1):
                # if erase_checkpoint_line=True and b1 != b2 only due to checkpoint info line, then data is still valid. Checkpoint info line is removed
                if erase_checkpoint_line and prev_b1+b1 == b'\n[':
                    fp1.seek(-2, os.SEEK_CUR)
                    fp1.truncate()
                    return True
                return False
            if not b1:
                return True
            prev_b1 = b1

def add_parser_args(parser):
    parser.add_argument('-a','--app_id', help="app id (i.e., Azure storage blob container name)", required=True)
    parser.add_argument('-l','--log_dir', help="base dir to download data (a subfolder will be created)", required=True)
    parser.add_argument('-cs','--conn_string', help="storage account connection string", required=False)
    parser.add_argument('-cn','--container', help="storage container name", required=False)
    parser.add_argument('--account_name', help="storage account name", required=False)
    parser.add_argument('--sas_token', help="storage account sas token to a container", required=False)
    parser.add_argument('-s','--start_date', help="downloading start date (included) - format YYYY-MM-DD", type=valid_date)
    parser.add_argument('-e','--end_date', help="downloading end date (included) - format YYYY-MM-DD", type=valid_date)
    parser.add_argument('-o','--overwrite_mode', type=int, help='''    0: never overwrite; ask the user whether blobs are currently used [default]
    1: ask the user how to proceed when the files have different sizes or when the blobs are currently being used
    2: always overwrite; download currently used blobs
    3: never overwrite; append if the size is larger, without asking; download currently used blobs
    4: never overwrite; append if the size is larger, without asking; skip currently used blobs''', default=0)
    parser.add_argument('--dry_run', help="print which blobs would have been downloaded, without downloading", action='store_true')
    parser.add_argument('--create_gzip_mode', type=int, help='''Mode to create gzip file(s) for Vowpal Wabbit:
    0: create one gzip file for each LastConfigurationEditDate prefix
    1: create a unique gzip file by merging over file dates
    2: create a unique gzip file by uniquing over EventId and sorting by Timestamp''', default=-1)
    parser.add_argument('--delta_mod_t', type=int, default=3600, help='time window in sec to detect if a file is currently in use (default=3600 - 1 hour)')
    parser.add_argument('--max_connections', type=int, default=4, help='number of max_connections (default=4)')
    parser.add_argument('--verbose', help="print more details", action='store_true')
    parser.add_argument('--confirm', help="confirm before downloading", action='store_true')
    parser.add_argument('--report_progress', help="report progress while downloading", action='store_false')
    parser.add_argument('--if_match', help="set get_blob_to_path() if_match field")
    parser.add_argument('-v','--version', type=int, default=2, help='''version of log downloader to use:
    1: for uncooked logs (only for backward compatibility) [deprecated]
    2: for cooked logs [default]''')

def update_progress(current, total):
    barLength = 50 # Length of the progress bar
    progress = current/total
    block = int(barLength*progress)
    Logger.info("\rProgress: [{0}] {1:.1f}%".format( "#"*block + "-"*(barLength-block), progress*100))

def download_container(app_id, log_dir, container=None, conn_string=None, account_name=None, sas_token=None, start_date=None, end_date=None, overwrite_mode=0, dry_run=False, version=2, verbose=False, create_gzip_mode=-1, delta_mod_t=3600, max_connections=4, confirm=False, report_progress=True, if_match=None):
    t_start = time.time()
    if not container:
        container=app_id

    print('------'*10)
    Logger.info('Current UTC time: {}\n'.format(datetime.datetime.now(datetime.timezone.utc)) + 
                'app_id: {}\n'.format(app_id) +
                'container: {}\n'.format(container) +
                'log_dir: {}\n'.format(log_dir) +
                'Start Date: {}\n'.format(start_date) +
                'End Date: {}\n'.format(end_date) +
                'Overwrite mode: {}\n'.format(overwrite_mode) +
                'dry_run: {}\n'.format(dry_run) +
                'version: {}\n'.format(version) +
                'create_gzip_mode: {}\n'.format(create_gzip_mode))
    print('------'*10)

    if not dry_run:
        os.makedirs(os.path.join(log_dir, app_id), exist_ok=True)

    output_fp = None
    total_download_size_MB = 0.0
    if version == 1: # using C# api for uncooked logs
        output_fp = os.path.join(log_dir, app_id, app_id+'_'+start_date.strftime("%Y-%m-%d")+'_'+end_date.strftime("%Y-%m-%d")+'.json')
        Logger.info('Destination: {}'.format(output_fp))
        do_download = True
        if os.path.isfile(output_fp):
            if overwrite_mode in {0, 3, 4}:
                Logger.info('Output file {} already exists. Not downloading'.format(output_fp))
                do_download = False
            elif overwrite_mode == 1 and input('Output file {} already exists. Do you want to overwrite [Y/n]? '.format(output_fp)) not in {'Y', 'y'}:
                do_download = False
                
        if do_download:
            if dry_run:
                Logger.info('--dry_run - Not downloading!')
            else:
                Logger.info('Downloading output file')
                try:
                    import requests
                    LogDownloaderURL = "https://cps-staging-exp-experimentation.azurewebsites.net/api/Log?account={ACCOUNT_NAME}&key={ACCOUNT_KEY}&start={START_DATE}&end={END_DATE}&container={CONTAINER}"
                    conn_string_dict = dict(x.split('=',1) for x in conn_string.split(';'))
                    if not conn_string_dict['AccountName'] or len(conn_string_dict['AccountKey']) != 88:
                        Logger.error("Invalid Azure Storage ConnectionString.")
                        sys.exit(1)
                    url = LogDownloaderURL.format(ACCOUNT_NAME=conn_string_dict['AccountName'], ACCOUNT_KEY=conn_string_dict['AccountKey'].replace('+','%2b'), CONTAINER=container, START_DATE=start_date.strftime("%Y-%m-%d"), END_DATE=(end_date+datetime.timedelta(days=1)).strftime("%Y-%m-%d"))
                    r = requests.post(url)
                    open(output_fp, 'wb').write(r.content)
                    Logger.info('Finished downloading output file')
                except:
                    Logger.exception()
                    sys.exit(1)
        
    else: # using BlockBlobService python api for cooked logs
        try:
            if sas_token and account_name:
                Logger.info('Establishing Azure Storage BlockBlobService connection using sas token...')
                bbs = BlockBlobService(account_name=account_name, sas_token=sas_token)
            else:
                Logger.info('Establishing Azure Storage BlockBlobService connection using connection string...')
                bbs = BlockBlobService(connection_string=conn_string)
            # List all blobs and download them one by one
            Logger.info('Getting blobs list...')
            blobs = bbs.list_blobs(container)
        except Exception as e:
            if e.args[0] == 'dictionary update sequence element #0 has length 1; 2 is required':
                Logger.error("Invalid Azure Storage ConnectionString.")
            elif type(e.args[0]) == str and e.args[0].startswith('The specified container does not exist.'):
                Logger.error("The specified container ({}) does not exist.".format(container))
            else:
                Logger.error("\nType: {}\nArgs: {}".format(type(e).__name__, e.args))
            sys.exit(1)

        Logger.info('Iterating through blobs...\n')
        selected_fps = []

        for blob in blobs:
            if '/data/' not in blob.name:
                if verbose:
                    Logger.info('{} - Skip: Non-data blob\n'.format(blob.name))
                continue

            configFolder = blob.name.split('/data/', 1)[0]
            if(bbs.exists(container, configFolder + "/checkpoint/imitationModeMetrics.json")):
                if verbose:
                    Logger.info('{} - Skip: imitation mode detected for configuration.\n'.format(blob.name))
                continue

            blob_day = datetime.datetime.strptime(blob.name.split('/data/', 1)[1].split('_', 1)[0], '%Y/%m/%d')
            if (start_date and blob_day < start_date) or (end_date and end_date < blob_day):
                if verbose:
                    Logger.info('{} - Skip: Outside of date range\n'.format(blob.name))
                continue

            try:
                bp = bbs.get_blob_properties(container, blob.name)

                if confirm:
                    if input("{} - Do you want to download [Y/n]? ".format(blob.name)) not in {'Y', 'y'}:
                        print()
                        continue

                fp = os.path.join(log_dir, app_id, blob.name.replace('/','_'))
                selected_fps.append(fp)
                if os.path.isfile(fp):
                    file_size = os.path.getsize(fp)
                    if overwrite_mode == 0:
                        if verbose:
                            Logger.info('{} - Skip: Output file already exists\n'.format(blob.name))
                        continue
                    elif overwrite_mode in {1, 3, 4}:
                        if file_size == bp.properties.content_length: # file size is the same, skip!
                            if verbose:
                                Logger.info('{} - Skip: Output file already exists with same size\n'.format(blob.name))
                            continue
                        Logger.info('Output file already exists: {}\nLocal size: {:.3f} MB\nAzure size: {:.3f} MB'.format(fp, file_size/(1024**2), bp.properties.content_length/(1024**2)))
                        if overwrite_mode in {3, 4} and file_size > bp.properties.content_length: # local file size is larger, skip with warning!
                            Logger.info('{} - Skip: Output file already exists with larger size\n'.format(blob.name))
                            continue
                        if overwrite_mode == 1 and input("Do you want to overwrite [Y/n]? ") not in {'Y', 'y'}:
                            print()
                            continue
                else:
                    file_size = None

                Logger.info('Processing: {} (size: {:.3f}MB - Last modified: {})'.format(blob.name, bp.properties.content_length/(1024**2), bp.properties.last_modified))
                # check if blob was modified in the last delta_mod_t sec
                if datetime.datetime.now(datetime.timezone.utc)-bp.properties.last_modified < datetime.timedelta(0, delta_mod_t):
                    if overwrite_mode < 2:
                        if input("Azure blob currently in use (modified in the last delta_mod_t={} sec). Do you want to download anyway [Y/n]? ".format(delta_mod_t)) not in {'Y', 'y'}:
                            print()
                            continue
                    elif overwrite_mode == 4:
                        Logger.info('Azure blob currently in use (modified in the last delta_mod_t={} sec). Skipping!\n'.format(delta_mod_t))
                        continue
                    if if_match != '*':     # when if_match is not '*', reset max_connections to 1 to prevent crash if azure blob is modified during download
                        max_connections = 1

                if dry_run:
                    Logger.info('--dry_run - Not downloading!')
                else:
                    t0 = time.time()
                    process_checker = update_progress if report_progress == True else None
                    if overwrite_mode in {3, 4} and file_size:
                        temp_fp = fp + '.temp'
                        cmpsize = min(file_size,8*1024**2)
                        bbs.get_blob_to_path(container, blob.name, temp_fp, max_connections=max_connections, start_range=file_size-cmpsize, end_range=file_size-1, if_match=if_match)
                        if cmp_files(fp, temp_fp, -cmpsize):
                            Logger.info('Check validity of remote file...Valid!')
                            Logger.info('Resume downloading to temp file with max_connections = {}...'.format(max_connections))
                            bbs.get_blob_to_path(container, blob.name, temp_fp, progress_callback=process_checker, max_connections=max_connections, start_range=os.path.getsize(fp), if_match=if_match)
                            download_time = time.time()-t0
                            download_size_MB = os.path.getsize(temp_fp)/(1024**2) # file size in MB
                            total_download_size_MB+=download_size_MB
                            Logger.info('\nAppending to local file...')
                            with open(fp, 'ab') as f1, open(temp_fp, 'rb') as f2:
                                shutil.copyfileobj(f2, f1, length=100*1024**2)   # writing chunks of 100MB to avoid consuming memory
                            Logger.info('Appending completed. Deleting temp file...')
                            os.remove(temp_fp)
                        else:
                            os.remove(temp_fp)
                            Logger.info('Check validity of remote file...Invalid! - Skip\n')
                            continue
                        Logger.info('Downloaded {:.3f} MB in {:.1f} sec. ({:.3f} MB/sec) - Total elapsed time: {:.1f} sec.\n'.format(download_size_MB, download_time, download_size_MB/download_time, time.time()-t0))
                    else:
                        Logger.info('Downloading with max_connections = {}...'.format(max_connections))
                        bbs.get_blob_to_path(container, blob.name, fp, progress_callback=process_checker, max_connections=max_connections, if_match=if_match)
                        download_time = time.time()-t0
                        download_size_MB = os.path.getsize(fp)/(1024**2) # file size in MB
                        total_download_size_MB+=download_size_MB
                        Logger.info('\nDownloaded {:.3f} MB in {:.1f} sec. ({:.3f} MB/sec)\n'.format(download_size_MB, download_time, download_size_MB/download_time))
            except:
                Logger.exception()

        if create_gzip_mode > -1:
            if selected_fps:
                selected_fps = [x for x in selected_fps if os.path.isfile(x)]
                if create_gzip_mode == 0:
                    models = {}
                    for fp in selected_fps:
                        models.setdefault(os.path.basename(fp).split('_data_',1)[0], []).append(fp)
                    for model in models:
                        models[model].sort(key=lambda x : list(map(int,x.split('_data_')[1].split('_')[:3])))
                        start_date = '-'.join(models[model][0].split('_data_')[1].split('_')[:3])
                        end_date = '-'.join(models[model][-1].split('_data_')[1].split('_')[:3])
                        output_fp = os.path.join(log_dir, app_id, app_id+'_'+model+'_data_'+start_date+'_'+end_date+'.json.gz')
                        Logger.info('Concat and zip files of LastConfigurationEditDate={} to: {}'.format(model, output_fp))
                        if os.path.isfile(output_fp) and __name__ == '__main__' and input('Output file already exists. Do you want to overwrite [Y/n]? '.format(output_fp)) not in {'Y', 'y'}:
                            continue
                        if dry_run:
                            Logger.info('--dry_run - Not downloading!')
                        else:
                            with gzip.open(output_fp, 'wb') as f_out:
                                for fp in models[model]:
                                    Logger.info('Adding: {}'.format(fp))
                                    with open(fp, 'rb') as f_in:
                                        shutil.copyfileobj(f_in, f_out, length=100*1024**2)   # writing chunks of 100MB to avoid consuming memory
                elif create_gzip_mode == 1:
                    selected_fps.sort(key=lambda x : (list(map(int,x.split('_data_')[1].split('_')[:3])), -os.path.getsize(x), x))
                    selected_fps_merged = []
                    last_fp_date = None
                    for fp in selected_fps:
                        fp_date = datetime.datetime.strptime('_'.join(fp.split('_data_')[1].split('_')[:3]), "%Y_%m_%d")
                        if fp_date != last_fp_date:
                            selected_fps_merged.append(fp)
                            last_fp_date = fp_date

                    start_date = '-'.join(selected_fps_merged[0].split('_data_')[1].split('_')[:3])
                    end_date = '-'.join(selected_fps_merged[-1].split('_data_')[1].split('_')[:3])
                    output_fp = os.path.join(log_dir, app_id, app_id+'_merged_data_'+start_date+'_'+end_date+'.json.gz')
                    Logger.info('Merge and zip files of all LastConfigurationEditDate to: {}'.format(output_fp))
                    if not os.path.isfile(output_fp) or __name__ == '__main__' and input('Output file already exists. Do you want to overwrite [Y/n]? '.format(output_fp)) in {'Y', 'y'}:
                        if dry_run:
                            for fp in selected_fps_merged:
                                Logger.info('Adding: {}'.format(fp))
                            Logger.info('--dry_run - Not downloading!')
                        else:
                            with gzip.open(output_fp, 'wb') as f_out:
                                for fp in selected_fps_merged:
                                    Logger.info('Adding: {}'.format(fp))
                                    with open(fp, 'rb') as f_in:
                                        shutil.copyfileobj(f_in, f_out, length=1024**3)   # writing chunks of 1GB to avoid consuming memory
                elif create_gzip_mode == 2:
                    selected_fps.sort(key=lambda x : (list(map(int,x.split('_data_')[1].split('_')[:3])), -os.path.getsize(x), x))
                    start_date = '-'.join(selected_fps[0].split('_data_')[1].split('_')[:3])
                    end_date = '-'.join(selected_fps[-1].split('_data_')[1].split('_')[:3])
                    output_fp = os.path.join(log_dir, app_id, app_id+'_deepmerged_data_'+start_date+'_'+end_date+'.json.gz')
                    Logger.info('Merge, unique, sort, and zip files of all LastConfigurationEditDate to: {}'.format(output_fp))
                    if not os.path.isfile(output_fp) or __name__ == '__main__' and input('Output file already exists. Do you want to overwrite [Y/n]? '.format(output_fp)) in {'Y', 'y'}:
                        d = {}
                        for fn in selected_fps:
                            Logger.info('Parsing: {}'.format(fn))
                            if not dry_run:
                                for x in open(fn, 'rb'):
                                    if x.startswith(b'{"_label_cost') and x.strip().endswith(b'}'):     # reading only cooked lined
                                        data = ds_parse.json_cooked(x)
                                        if data['ei'] not in d or float(data['cost']) < d[data['ei']][1]: # taking line with best reward
                                            d[data['ei']] = (data['ts'], float(data['cost']), x)
                            Logger.info(' - len(d): {}'.format(len(d)))

                        Logger.info('Writing to output .gz file...')
                        if dry_run:
                            Logger.info('--dry_run - Not downloading!')
                        else:
                            with gzip.open(output_fp, 'wb') as f:
                                i = 0
                                for x in sorted(d.values(), key=lambda x : x[0]):                       # events are sorted by timestamp
                                    f.write(x[2])
                                    i += 1
                                    if i % 5000 == 0:
                                        update_progress(i, len(d))
                                update_progress(i, len(d))
                                print()
                else:
                    Logger.warning('Unrecognized --create_gzip_mode: {}, skipping creating gzip files.'.format(create_gzip_mode))
            else:
                Logger.info('No file downloaded, skipping creating gzip files.')
                    
    time_taken = time.time()-t_start
    Logger.info('Total elapsed time: {:.1f} sec.\n'.format(time_taken))
    Logger.info('\nDownloaded {:.3f} MB in {:.1f} sec. ({:.3f} MB/sec)\n'.format(total_download_size_MB, time_taken, total_download_size_MB/time_taken))
    return output_fp, total_download_size_MB

if __name__ == '__main__':
    parser = argparse.ArgumentParser(formatter_class=argparse.RawTextHelpFormatter)
    add_parser_args(parser)
    kwargs = vars(parser.parse_args(sys.argv[1:]))
    if kwargs['version'] == 1:
        if kwargs.get('start_date', None) is None:
            parser.error('When downloading using version=1, the following argument is required: --start_date')
        
        if kwargs.get('end_date', None) is None:
            kwargs['end_date'] = datetime.datetime.utcnow() 

    # Parse ds.config
    auth_dict = dict(x.split(': ',1) for x in open('ds.config').read().split('[AzureStorageAuthentication]',1)[1].split('\n') if ': ' in x)
    auth_str = auth_dict.get(kwargs['app_id'], auth_dict['$Default'])
    kwargs.update(dict(x.split(':',1) for x in auth_str.split(',')))

    download_container(**kwargs)