from azure.storage.blob import BlockBlobService
import pickle, os, time, sys, datetime, requests, argparse
import configparser

LogDownloaderURL = "https://cps-staging-exp-experimentation.azurewebsites.net/api/Log?account={ACCOUNT_NAME}&key={ACCOUNT_KEY}&start={START_DATE}&end={END_DATE}&container={CONTAINER}"


def valid_date(s):
    try:
        return datetime.datetime.strptime(s, "%Y-%m-%d")
    except ValueError:
        raise argparse.ArgumentTypeError("Not a valid date: '{0}'. Expected format: YYYY-MM-DD".format(s))

def parse_argv(argv):

    parser = argparse.ArgumentParser()
    parser.add_argument('-c','--container', help="container name", required=True)
    parser.add_argument('-l','--log_dir', help="base dir to download data", required=True)
    parser.add_argument('-s','--start_date', help="downloading start date (included) - format YYYY-MM-DD", type=valid_date)
    parser.add_argument('-e','--end_date', help="downaloading end date (not included) - format YYYY-MM-DD", type=valid_date)
    parser.add_argument('-v','--version', type=int, default=2, help="integer describing which version of data downloader to use (default: 2 -> AzureStorageDownloader)")
    parser.add_argument('-o','--overwrite_mode', type=int, help="0: don't overwrite (default); 1: ask user if files have different sizes; 2: always overwrite", default=0)
    parser.add_argument('-a','--auth_fp', default='', help="file path of pickle file containing dictionary for Azure storage authentication (when missing, ds.config info is used instead). Pickle dictionary format: {container : {'ACCOUNT' : AccountName, 'KEY' : AccountKey}}")
    parser.add_argument('--dry_run', help="print which blobs would have been downloaded, without downloading", action='store_true')
    parser.add_argument('--skip_current', help="avoid downloading a blob which has been modified within the last 10 min", action='store_true')
    parser.add_argument('--verbose', action='store_true')
        
    kwargs = vars(parser.parse_args(argv[1:]))
    if len(argv) > 5:
        if kwargs.get('start_date', None) is None:
            parser.error('When downloading, the following argument is required: --start_date')
        
        if kwargs.get('end_date', None) is None:
            kwargs['end_date'] = datetime.datetime.utcnow() + datetime.timedelta(days=1) # filling end_date as tomorrow in UTC
        
        kwargs['output_fp'] = os.path.join(kwargs['log_dir'], kwargs['container'], kwargs['container']+'_'+kwargs['start_date'].strftime("%Y-%m-%d")+'_'+kwargs['end_date'].strftime("%Y-%m-%d")+'.json')

    return kwargs

def update_progress(current, total):
    barLength = 50 # Length of the progress bar
    progress = current/total
    block = int(barLength*progress)
    text = "\rProgress: [{0}] {1:.1f}%".format( "#"*block + "-"*(barLength-block), progress*100)
    sys.stdout.write(text)
    sys.stdout.flush()

def download_container(container, log_dir, start_date=None, end_date=None, overwrite_mode=0, dry_run=False, skip_current=False, version=2, auth_fp='', output_fp='', verbose=False):
    
    print('Start Date: {}'.format(start_date))
    print('End Date: {}'.format(end_date))
    print('Overwrite mode: {}'.format(overwrite_mode))
    print('dry_run: {}'.format(dry_run))
    print('skip_current: {}'.format(skip_current))
    print('version: {}'.format(version))
    
    if not dry_run and not os.path.isdir(os.path.join(log_dir, container)):
        os.makedirs(os.path.join(log_dir, container))
    
    # Get Azure Storage Autentication
    if os.path.isfile(auth_fp):
        print('Using pickle file for Azure Storage Autentication')
        try:
            with open(auth_fp, 'rb') as pkl_file:
                auth_data = pickle.load(pkl_file)
            if container not in auth_data:
                print("Container {} not in data dict".format(container))
                sys.exit()
            account_name = auth_data[container]['ACCOUNT']
            account_key = auth_data[container]['KEY']
        except Exception as e:
            print("Error reading data dict from {}: {}".format(auth_fp,e))
            sys.exit()
    else:
        print('Using ds.config file for Azure Storage Autentication')
        config = configparser.ConfigParser()
        config.read('ds.config')
        account_name = config['DecisionService']['AzureBlobStorageAccountName']
        account_key = config['DecisionService']['AzureBlobStorageAccountKey']
    
    if len(account_key.replace('%2b','+')) != 88:
        print("Invalid Azure Storage Account Key!")
        sys.exit()
    
    if version == 1: # using LogDownloader api
        if overwrite_mode < 2 and os.path.isfile(output_fp):
            print('File ({}) already exits, not downloading'.format(output_fp))
        else:
            print('Destination: {}\nDownloading...'.format(output_fp), end='')
            if dry_run:
                print(' dry_run!')
            else:
                try:
                    url = LogDownloaderURL.format(ACCOUNT_NAME=account_name, ACCOUNT_KEY=account_key.replace('+','%2b'), CONTAINER=container, START_DATE=start_date.strftime("%Y-%m-%d"), END_DATE=end_date.strftime("%Y-%m-%d"))
                    r = requests.post(url)
                    open(output_fp, 'wb').write(r.content)
                    print(' Done!\n')
                except Exception as e:
                    print(' Error: {}'.format(e))
        
    else: # using BlockBlobService python api
        bbs = BlockBlobService(account_name=account_name, account_key=account_key.replace('%2b','+'))

        # List all blobs and download them one by one
        blobs = bbs.list_blobs(container)
        for blob in blobs:
            if '/data/' not in blob.name:
                if verbose:
                    print('Skip non-data file: {}'.format(blob.name))
                continue
            
            blob_day = datetime.datetime.strptime(blob.name.split('/data/', 1)[1].split('_', 1)[0], '%Y/%m/%d')
            if (start_date and blob_day < start_date) or (end_date and end_date <= blob_day):
                continue

            try:
                bp = bbs.get_blob_properties(container, blob.name)
                curr_time = datetime.datetime.now(datetime.timezone.utc)
                if skip_current and curr_time-bp.properties.last_modified < datetime.timedelta(0, 600, 0):    # skip blob if not modified in the last 10 min
                    if verbose:
                        print('Skip current - Time delta: {}'.format(curr_time-bp.properties.last_modified))
                    continue

                output_fp = os.path.join(log_dir, container, blob.name.replace('/','_'))
                if overwrite_mode < 2 and os.path.isfile(output_fp):
                    if overwrite_mode == 0:
                        if verbose:
                            print('Output file already exits - Skip: {}'.format(output_fp))
                        continue
                    elif overwrite_mode == 1:
                        file_size = os.path.getsize(output_fp)/(1024**2) # file size in MB
                        if file_size == bp.properties.content_length/(1024**2): # file size is the same, skip!
                            if verbose:
                                print('Skip - Files have same size')
                            continue
                        print('Output file already exits: {}\nLocal size: {:.3f} MB\nAzure size: {:.3f} MB'.format(output_fp, file_size, bp.properties.content_length/(1024**2)))
                        if input("Do you want to overwrite [Y/n]? ") != 'Y':
                            continue

                print('\nBlob name: {}\nBlob size: {:.3f} MB\nLast modified: {}\nDestination: {}'.format(blob.name, bp.properties.content_length/(1024**2), bp.properties.last_modified, output_fp))
                if dry_run:
                    print('dry_run - Not downloading!')
                else:
                    t0 = time.time()
                    bbs.get_blob_to_path(container, blob.name, output_fp, progress_callback=update_progress)
                    elapsed_time = time.time()-t0
                    file_size = os.path.getsize(output_fp)/(1024**2) # file size in MB
                    print('\nDownloaded {:.3f} MB in {:.3f} sec.: Average: {:.3f} MB/sec\n'.format(file_size, elapsed_time, file_size/elapsed_time))
            except Exception as e:
                print(' Error: {}'.format(e))


if __name__ == '__main__':
    
    kwargs = parse_argv(sys.argv)
    print('Container: {0}'.format(kwargs['container']))
    print('log_dir: {0}'.format(kwargs['log_dir']))

    ################################# PARSE INPUT CMD #########################################################
    
    t0 = time.time()
    download_container(**kwargs)
    print('Total download time:',time.time()-t0)
