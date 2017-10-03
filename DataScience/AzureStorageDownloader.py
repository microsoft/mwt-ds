from azure.storage.blob import BlockBlobService
import pickle, os, time, sys, datetime
import gzip
import configparser


def download_container(CONTAINER_NAME, LOG_DIR, ACCOUNT_NAME, ACCOUNT_KEY, start_date=None, end_date=None, overwrite=False, dry_run=False, skip_current=False):
    
    print("Container: {}".format(CONTAINER_NAME))

    bbs = BlockBlobService(account_name=ACCOUNT_NAME, account_key=ACCOUNT_KEY)

    if not os.path.isdir(os.path.join(LOG_DIR, CONTAINER_NAME)):
        os.makedirs(os.path.join(LOG_DIR, CONTAINER_NAME))

    # List all blobs and download them one by one
    blobs = bbs.list_blobs(CONTAINER_NAME)
    for blob in blobs:
        if '/data/' not in blob.name:
            continue
        fp = os.path.join(LOG_DIR, CONTAINER_NAME, blob.name.replace('/','_')+'.gz')
        if os.path.isfile(fp) and not overwrite:
            continue
        
        blob_day = datetime.datetime.strptime(blob.name.split('/data/', 1)[1].split('_', 1)[0], '%Y/%m/%d')
        if (start_date and blob_day < start_date) or (end_date and end_date <= blob_day):
            continue

        try:
            bp = bbs.get_blob_properties(CONTAINER_NAME, blob.name)
            curr_time = datetime.datetime.now(datetime.timezone.utc)
            diff_time = curr_time-bp.properties.last_modified
            will_download = (not skip_current) or diff_time > datetime.timedelta(0, 30, 0)    # download blob if not modified in the last 30 secs
            if will_download:
                print("\nBlob {}\nUncompressed Size: {:.3f} MB\nLast modified: {}\nCurrent time: {}\nDiff from current time: {} - Will download: {}".format(blob.name, bp.properties.content_length/(1024**2), bp.properties.last_modified, curr_time, diff_time, will_download))
                print('Destination: {}\nDownloading...'.format(fp), end='')
                if dry_run:
                    print(' dry_run!')
                else:
                    t0 = time.time()
                    gzip.open(fp, 'wb').write(bbs.get_blob_to_bytes(CONTAINER_NAME, blob.name).content)
                    elapsed_time = time.time()-t0
                    file_size = os.path.getsize(fp)/(1024**2) # file size in MB
                    print(' Done! - {:.3f} MB in {:.3f} sec.: Average: {:.3f} MB/sec'.format(file_size, elapsed_time, file_size/elapsed_time))
        except Exception as e:
            print(' Error: {}'.format(e))


if __name__ == '__main__':
    
    usage_str = ("--------------------------------------------------------------------\n"
                 "Usage: python AzureStorageDownloader.py {container_name} [--start_date YYYY-MM-DD] [--end_date YYYY-MM-DD] [--overwrite] [--dry_run] [--skip_current]\n"
                 "--------------------------------------------------------------------\n"
                 "Notes:\n"
                 "end_date is not included\n"
                 "overwrite: would replace files on disk\n"
                 "dry_run: print which blobs would have been downloaded, without downloading\n"
                 "skip_current: if true, avoid downloading a blob which has been modified within the last 30 secs\n"
                 "--------------------------------------------------------------------")
    if len(sys.argv) < 2:
        print(usage_str)
        print("Example Usage: python AzureStorageDownloader.py cmplx --start_date 2017-10-15 --end_date 2017-10-18")
        sys.exit()
        
    config = configparser.ConfigParser()
    config.read('ds.config')
    visua_config = config['Visualization']
    
    log_dir = visua_config['LogDir']
    pkl_data_fp = visua_config['PickleDataPath']
    
            
    container = sys.argv[1]
    print('Container: {0}'.format(container))

    ################################# LOG DOWNLOADER #########################################################
    
    overwrite = False    
    dry_run = False
    start_date = None
    end_date = None
    skip_current = False
    try:
        if len(sys.argv) > 2:
            ii = 2
            while ii < len(sys.argv):
                if sys.argv[ii] == '--overwrite':
                    overwrite = True
                elif sys.argv[ii] == '--dry_run':
                    dry_run = True
                elif sys.argv[ii] == '--skip_current':
                    skip_current = True
                elif sys.argv[ii] == '--start_date':
                    ii += 1
                    start_date = datetime.datetime.strptime(sys.argv[ii], '%Y-%m-%d')
                elif sys.argv[ii] == '--end_date':
                    ii += 1
                    end_date = datetime.datetime.strptime(sys.argv[ii], '%Y-%m-%d')
                else:
                    print('Input arg: {} not recognized! Abort!'.format(sys.argv[ii]))
                    print(usage_str)
                    sys.exit()
                ii += 1
    except Exception as e:
        print('Error: {}'.format(e))
        print(usage_str)
        sys.exit()

    print('Overwrite: {0}'.format(overwrite))
    print('dry_run: {0}'.format(dry_run))
    print('Start Date: {0}'.format(start_date))
    print('End Date: {0}'.format(end_date))
    
    try:
        with open(pkl_data_fp, 'rb') as pkl_file:
            data = pickle.load(pkl_file)
    except Exception as e:
        print("Error reading data dict from {}: {}".format(pkl_data_fp,e))
        sys.exit()    

    if container not in data:
        print("Container {} not in data dict".format(container))
        sys.exit()

    t0 = time.time()
    download_container(container, log_dir, data[container]['ACCOUNT'], data[container]['KEY'].replace('%2b','+'), start_date, end_date, overwrite, dry_run, skip_current)
    print('Total download time:',time.time()-t0)
