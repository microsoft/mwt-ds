import sys, time, os, shutil
try:
    from azure.storage.blob import BlockBlobService
except ImportError as e:
    print('ImportError: {}'.format(e))
    if input("azure.storage.blob is required. Do you want to install it [Y/n]? ") in {'Y', 'y'}:
        import pip
        pip.main(['install', 'azure.storage.blob'])
        print('Please re-run script.')
    sys.exit()

def update_progress(current, total):
    barLength = 50 # Length of the progress bar
    progress = current/total
    block = int(barLength*progress)
    text = "\rProgress: [{0}] {1:.1f}%".format( "#"*block + "-"*(barLength-block), progress*100)
    sys.stdout.write(text)
    sys.stdout.flush()

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


########## PARAMETERS ############
sas_token =''
accountName = ''
connection_string=''
app_id = ''
blob_name = ''
log_dir = r''
#################################

os.makedirs(log_dir, exist_ok=True)

max_connections = 8
if_match = '*'

try:
    print('Establishing Azure Storage BlockBlobService connection...')
    # bbs = BlockBlobService(connection_string=connection_string)
    bbs = BlockBlobService(account_name=accountName, sas_token=sas_token)
except Exception as e:
    if e.args[0] == 'dictionary update sequence element #0 has length 1; 2 is required':
        print("Error: Invalid Azure Storage ConnectionString.")
    elif e.args[0].startswith('The specified container does not exist.'):
        print("Error: The specified container ({}) does not exist.".format(app_id))
    else:
        print("Error:\nType: {}\nArgs: {}".format(type(e).__name__, e.args))
    sys.exit()
    
# List all blobs and download them one by one
blobs = bbs.list_blobs(app_id)
downloaded = False
for x in blobs:
    print('File: {} (size: {:.3f}MB - Last modified: {})'.format(x.name, x.properties.content_length/(1024**2), x.properties.last_modified))
    if blob_name_str in x.name:
        fp = os.path.join(log_dir, x.name.replace('/','_'))
        if os.path.isfile(fp):
            file_size = os.path.getsize(fp)
            remote_file_size = x.properties.content_length
            
            if file_size == remote_file_size: # file size is the same, skip!
                print('{} - Skip: Output file already exits with same size\n'.format(x.name))
                continue
            print('Output file already exits: {}\nLocal size: {:.3f} MB\nAzure size: {:.3f} MB'.format(fp, file_size/(1024**2), remote_file_size/(1024**2)))
            if file_size > remote_file_size: # local file size is larger, skip with warning!
                print('{} - Skip: Output file already exits with larger size\n'.format(x.name))
                continue
            
            t0 = time.time()
            print('Check validity of remote file... ', end='')
            temp_fp = fp + '.temp'
            cmpsize = min(file_size,8*1024**2)
            bbs.get_blob_to_path(app_id, x.name, temp_fp, max_connections=max_connections, start_range=file_size-cmpsize, end_range=file_size-1,if_match=if_match)
            if cmp_files(fp, temp_fp, -cmpsize):
                print('Valid!')
                print('Resume downloading to temp file with max_connections = {}...'.format(max_connections))
                print('file_size:',file_size)
                print('os.path.getsize(fp):',os.path.getsize(fp))
                bbs.get_blob_to_path(app_id, x.name, temp_fp, progress_callback=update_progress, max_connections=max_connections, start_range=os.path.getsize(fp),if_match=if_match)
                download_time = time.time()-t0
                download_size_MB = os.path.getsize(temp_fp)/(1024**2) # file size in MB
                print('\nAppending to local file...')
                with open(fp, 'ab') as f1, open(temp_fp, 'rb') as f2:
                    shutil.copyfileobj(f2, f1, length=1024**3)   # writing chunks of 1GB to avoid consuming memory
                print('Appending completed. Deleting temp file...')
                os.remove(temp_fp)
            else:
                os.remove(temp_fp)
                print('Invalid! - Skip\n')
                continue
            print('Downloaded {:.3f} MB in {:.1f} sec. ({:.3f} MB/sec) - Total elapsed time: {:.1f} sec.\n'.format(download_size_MB,download_time, download_size_MB/download_time, time.time()-t0))
        else:
            print('Downloading with max_connections = {}...'.format(max_connections))
            file_size = x.properties.content_length
            bbs.get_blob_to_path(app_id, x.name, fp, progress_callback=update_progress, max_connections=max_connections, if_match=if_match)
