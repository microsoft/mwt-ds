import os, time, sys, datetime, requests, argparse, gzip
import configparser
try:
    from azure.storage.blob import BlockBlobService
except ImportError as e:
    print('ImportError: {}'.format(e))
    if input("azure.storage.blob is required. Do you want to install it [Y/n]? ") != 'Y':
        sys.exit()
    import pip
    pip.main(['install', 'azure.storage.blob'])


LogDownloaderURL = "https://cps-staging-exp-experimentation.azurewebsites.net/api/Log?account={ACCOUNT_NAME}&key={ACCOUNT_KEY}&start={START_DATE}&end={END_DATE}&container={CONTAINER}"


def valid_date(s):
    try:
        return datetime.datetime.strptime(s, "%Y-%m-%d")
    except ValueError:
        raise argparse.ArgumentTypeError("Not a valid date: '{0}'. Expected format: YYYY-MM-DD".format(s))

def parse_argv(argv):

    parser = argparse.ArgumentParser()
    parser.add_argument('-a','--app_id', help="app id (aka Azure storage container name)", required=True)
    parser.add_argument('-l','--log_dir', help="base dir to download data", required=True)
    parser.add_argument('-s','--start_date', help="downloading start date (included) - format YYYY-MM-DD", type=valid_date)
    parser.add_argument('-e','--end_date', help="downloading end date (not included) - format YYYY-MM-DD (default: tomorrow's date)", type=valid_date)
    parser.add_argument('-v','--version', type=int, default=2, help="integer describing which version of data downloader to use (default: 2 -> AzureStorageDownloader)")
    parser.add_argument('-o','--overwrite_mode', type=int, help="0: don't overwrite (default); 1: ask user if files have different sizes; 2: always overwrite", default=0)
    parser.add_argument('--dry_run', help="print which blobs would have been downloaded, without downloading", action='store_true')
    parser.add_argument('--no_gzip', help="Skip producing gzip file for Vowpal Wabbit", action='store_true')
    parser.add_argument('--verbose', action='store_true')
        
    kwargs = vars(parser.parse_args(argv[1:]))
    if len(argv) > 5:
        if kwargs.get('start_date', None) is None:
            parser.error('When downloading, the following argument is required: --start_date')
        
        if kwargs.get('end_date', None) is None:
            kwargs['end_date'] = datetime.datetime.utcnow() + datetime.timedelta(days=1) # filling end_date as tomorrow in UTC
        
        kwargs['output_fp'] = os.path.join(kwargs['log_dir'], kwargs['app_id'], kwargs['app_id']+'_'+kwargs['start_date'].strftime("%Y-%m-%d")+'_'+kwargs['end_date'].strftime("%Y-%m-%d")+'.json')

    return kwargs

def update_progress(current, total):
    barLength = 50 # Length of the progress bar
    progress = current/total
    block = int(barLength*progress)
    text = "\rProgress: [{0}] {1:.1f}%".format( "#"*block + "-"*(barLength-block), progress*100)
    sys.stdout.write(text)
    sys.stdout.flush()

def download_container(app_id, log_dir, start_date=None, end_date=None, overwrite_mode=0, dry_run=False, version=2, auth_fp=None, output_fp='', verbose=False, no_gzip=False):
    
    t_start = time.time()
    print('-----'*10)
    print('Current UTC time: {}'.format(datetime.datetime.now(datetime.timezone.utc)))
    print('Start Date: {}'.format(start_date))
    print('End Date: {}'.format(end_date))
    print('Overwrite mode: {}'.format(overwrite_mode))
    print('dry_run: {}'.format(dry_run))
    print('version: {}'.format(version))
    print('no_gzip: {}'.format(no_gzip))
    
    if not dry_run and not os.path.isdir(os.path.join(log_dir, app_id)):
        os.makedirs(os.path.join(log_dir, app_id))
    
    # Get Azure Storage Authentication
    config = configparser.ConfigParser()
    config.read('ds.config')
    connection_string = config['AzureStorageAuthentication'].get(app_id, config['AzureStorageAuthentication']['$Default'])
    # Check connection string (and parse for logDownloader)
    try:
        connection_string_dict = {x.split('=',1)[0] : x.split('=',1)[1] for x in connection_string.split(';')}
        if not connection_string_dict['AccountName'] or len(connection_string_dict['AccountKey']) != 88:
            raise
    except:
        print("Error: Invalid Azure Storage ConnectionString.")
        sys.exit()
    
    print('-----'*10)
    
    if version == 1: # using LogDownloader api
        if overwrite_mode < 2 and os.path.isfile(output_fp):
            print('{} already exits, not downloading'.format(output_fp))
        else:
            print('Destination: {}'.format(output_fp))
            if dry_run:
                print('--dry_run - Not downloading!')
            else:
                print('Downloading...'.format(output_fp), end='')
                try:
                    url = LogDownloaderURL.format(ACCOUNT_NAME=connection_string_dict['AccountName'], ACCOUNT_KEY=connection_string_dict['AccountKey'].replace('+','%2b'), CONTAINER=app_id, START_DATE=start_date.strftime("%Y-%m-%d"), END_DATE=end_date.strftime("%Y-%m-%d"))
                    r = requests.post(url)
                    open(output_fp, 'wb').write(r.content)
                    print(' Done!\n')
                except Exception as e:
                    print(' Error: {}'.format(e))
        
    else: # using BlockBlobService python api
    
        output_fps = []
        bbs = BlockBlobService(connection_string=connection_string)

        # List all blobs and download them one by one
        print('Getting blobs list...', end='', flush=True)
        blobs = bbs.list_blobs(app_id)
        print(' Done!\nIterating through blobs...\n')
        for blob in blobs:
            if '/data/' not in blob.name:
                if verbose:
                    print('{} - Skip: Non-data blob'.format(blob.name))
                continue
            
            blob_day = datetime.datetime.strptime(blob.name.split('/data/', 1)[1].split('_', 1)[0], '%Y/%m/%d')
            if (start_date and blob_day < start_date) or (end_date and end_date <= blob_day):
                if verbose:
                    print('{} - Skip: Outside of date range'.format(blob.name))
                continue

            try:
                bp = bbs.get_blob_properties(app_id, blob.name)

                fp = os.path.join(log_dir, app_id, blob.name.replace('/','_'))
                output_fps.append(fp)
                if overwrite_mode < 2 and os.path.isfile(fp):
                    if overwrite_mode == 0:
                        if verbose:
                            print('{} - Skip: Output file already exits'.format(blob.name))
                        continue
                    elif overwrite_mode == 1:
                        file_size = os.path.getsize(fp)/(1024**2) # file size in MB
                        if file_size == bp.properties.content_length/(1024**2): # file size is the same, skip!
                            if verbose:
                                print('{} - Skip: Output file already exits with same size'.format(blob.name))
                            continue
                        print('Output file already exits: {}\nLocal size: {:.3f} MB\nAzure size: {:.3f} MB'.format(fp, file_size, bp.properties.content_length/(1024**2)))
                        if input("Do you want to overwrite [Y/n]? ") != 'Y':
                            continue

                print('Processing: {} (size: {:.3f}MB - Last modified: {})'.format(blob.name, bp.properties.content_length/(1024**2), bp.properties.last_modified))
                if dry_run:
                    print('--dry_run - Not downloading!')
                else:
                    # check if blob was modified within the last 1 hour
                    if datetime.datetime.now(datetime.timezone.utc)-bp.properties.last_modified < datetime.timedelta(0, 3600):
                        if overwrite_mode < 2 and input("Azure blob currently in use (modified during last hour). Do you want to download anyway [Y/n]? ") != 'Y':
                            continue
                        max_connections = 1 # set max_connections to 1 to prevent crash if azure blob is modified during download
                    else:
                        max_connections = 4
                    print('Downloading...')
                    t0 = time.time()
                    bbs.get_blob_to_path(app_id, blob.name, fp, progress_callback=update_progress, max_connections=max_connections)
                    elapsed_time = time.time()-t0
                    file_size = os.path.getsize(fp)/(1024**2) # file size in MB
                    print('\nDownloaded {:.3f} MB in {:.3f} sec.: Average: {:.3f} MB/sec'.format(file_size, elapsed_time, file_size/elapsed_time))                    
            except Exception as e:
                print(' Error: {}'.format(e))

        if not dry_run and not no_gzip:
            print('Concat and zip files to: {}'.format(output_fp+'.gz'))
            output_fps.sort(key=lambda x : (len(x),x))
            with gzip.open(output_fp+'.gz', 'wb') as f_out:
                for fp in output_fps:
                    if os.path.isfile(fp):
                        print('Adding: {}'.format(fp))
                        with open(fp, 'rb') as f_in:
                            f_out.write(f_in.read())
                    
    print('Total download time:',time.time()-t_start)


if __name__ == '__main__':
    
    kwargs = parse_argv(sys.argv)
    print('app_id: {0}'.format(kwargs['app_id']))
    print('log_dir: {0}'.format(kwargs['log_dir']))
    
    ################################# PARSE INPUT CMD #########################################################
    
    download_container(**kwargs)
