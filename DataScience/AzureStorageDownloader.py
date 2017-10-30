from azure.storage.blob import BlockBlobService
import pickle, os, time, sys, datetime, requests
import gzip
import configparser

LogDownloaderURL = "https://cps-staging-exp-experimentation.azurewebsites.net/api/Log?account={ACCOUNT_NAME}&key={ACCOUNT_KEY}&start={START_DATE}&end={END_DATE}&container={CONTAINER}"

def parse_argv(argv):

    usage_str = ("--------------------------------------------------------------------\n\n"
                 "Usage: python {} --container container_name --log_dir log_dir [--start_date YYYY-MM-DD] [--end_date YYYY-MM-DD] [--overwrite] [--dry_run] [--skip_current] [--version #] [--auth_fp file_path]\n"
                 "--------------------------------------------------------------------\n"
                 "Notes:\n"
                 "data from end_date is not included\n"
                 "overwrite: would replace files on disk\n"
                 "dry_run: print which blobs would have been downloaded, without downloading\n"
                 "skip_current: if true, avoid downloading a blob which has been modified within the last 10 min\n"
                 "version: integer describing which version of data downloader to use (default: 2 -> AzureStorageDownloader)"
                 "auth_fp: file path of pickle file containing azure storage authentication (when this flag is missing, ds.config info is used for authentication\n"
                 "--------------------------------------------------------------------".format(argv[0]))

    kwargs = {}
    try:
        ii = 1
        while ii < len(argv):
            if argv[ii] == '--container':
                ii += 1
                kwargs['container'] = argv[ii]
            elif argv[ii] == '--log_dir':
                ii += 1
                kwargs['log_dir'] = argv[ii]
            elif argv[ii] == '--overwrite':
                kwargs['overwrite'] = True
            elif argv[ii] == '--dry_run':
                kwargs['dry_run'] = True
            elif argv[ii] == '--skip_current':
                kwargs['skip_current'] = True
            elif argv[ii] == '--version':
                ii += 1
                kwargs['version'] = int(argv[ii])
            elif argv[ii] == '--auth_fp':
                ii += 1
                kwargs['auth_fp'] = argv[ii]
            elif argv[ii] == '--start_date':
                ii += 1
                kwargs['start_date'] = datetime.datetime.strptime(argv[ii], '%Y-%m-%d')
            elif argv[ii] == '--end_date':
                ii += 1
                kwargs['end_date'] = datetime.datetime.strptime(argv[ii], '%Y-%m-%d')
            else:
                print('Input arg: {} not recognized! Abort!'.format(argv[ii]))
                print(usage_str)
                sys.exit()
            ii += 1
        
        if 'container' not in kwargs:
            raise Exception('--container is a required input')
            
        if 'log_dir' not in kwargs:
            raise Exception('--log_dir is a required input')
            
        if len(kwargs) > 2:
            if 'start_date' not in kwargs:
                raise Exception('When downloading, --start_date is a required input')
            
            if 'end_date' not in kwargs:
                kwargs['end_date'] = datetime.datetime.utcnow() + datetime.timedelta(days=1) # filling end_date as tomorrow in UTC
            
            kwargs['output_fp'] = os.path.join(kwargs['log_dir'], kwargs['container'], kwargs['container']+'_'+kwargs['start_date'].strftime("%Y-%m-%d")+'_'+kwargs['end_date'].strftime("%Y-%m-%d")+'.json')
        
    except Exception as e:
        print('Error: {}'.format(e))
        print(usage_str)
        sys.exit()
        
    return kwargs

def download_container(container, log_dir, start_date=None, end_date=None, overwrite=False, dry_run=False, skip_current=False, version=2, auth_fp='', output_fp=''):
    
    print('Start Date: {}'.format(start_date))
    print('End Date: {}'.format(end_date))
    print('Overwrite: {}'.format(overwrite))
    print('dry_run: {}'.format(dry_run))
    print('skip_current: {}'.format(skip_current))
    
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
        if not overwrite and os.path.isfile(output_fp):
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
                    print(' Done!')
                except Exception as e:
                    print(' Error: {}'.format(e))
        
    else: # using BlockBlobService python api
        bbs = BlockBlobService(account_name=account_name, account_key=account_key.replace('%2b','+'))

        # List all blobs and download them one by one
        blobs = bbs.list_blobs(container)
        for blob in blobs:
            if '/data/' not in blob.name:
                continue
            fp = os.path.join(log_dir, container, blob.name.replace('/','_'))
            if os.path.isfile(fp) and not overwrite:
                continue
            
            blob_day = datetime.datetime.strptime(blob.name.split('/data/', 1)[1].split('_', 1)[0], '%Y/%m/%d')
            if (start_date and blob_day < start_date) or (end_date and end_date <= blob_day):
                continue

            try:
                bp = bbs.get_blob_properties(container, blob.name)
                curr_time = datetime.datetime.now(datetime.timezone.utc)
                diff_time = curr_time-bp.properties.last_modified
                will_download = (not skip_current) or diff_time > datetime.timedelta(0, 600, 0)    # download blob if not modified in the last 10 min
                if will_download:
                    print("\nBlob {}\nUncompressed Size: {:.3f} MB\nLast modified: {}\nCurrent time: {}\nDiff from current time: {} - Will download: {}".format(blob.name, bp.properties.content_length/(1024**2), bp.properties.last_modified, curr_time, diff_time, will_download))
                    print('Destination: {}\nDownloading...'.format(fp), end='')
                    if dry_run:
                        print(' dry_run!')
                    else:
                        t0 = time.time()
                        bbs.get_blob_to_path(container, blob.name, fp)
                        elapsed_time = time.time()-t0
                        file_size = os.path.getsize(fp)/(1024**2) # file size in MB
                        print(' Done! - {:.3f} MB in {:.3f} sec.: Average: {:.3f} MB/sec'.format(file_size, elapsed_time, file_size/elapsed_time))
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
