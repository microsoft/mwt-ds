import common
import configparser
from multiprocessing.dummy import Pool
import time
from azure.storage.blob import BlockBlobService
import ntpath
import os
import os.path
import sys
from datetime import date, datetime, timedelta
import subprocess

if __name__ == '__main__':

    start_time = time.time()

    config = configparser.ConfigParser()
    config.read('ds.config')
    ds = config['DecisionService']
    cache_folder = ds['CacheFolder']
    joined_examples_container = ds['JoinedExamplesContainer']

    # https://azure-storage.readthedocs.io/en/latest/_modules/azure/storage/blob/models.html#BlobBlock
    block_blob_service = BlockBlobService(account_name=ds['AzureBlobStorageAccountName'], account_key=ds['AzureBlobStorageAccountKey'])

    # Parse start and end dates for getting data
    if len(sys.argv) < 3:
        print("Start and end dates are expected. Example: python index_data.py 20161122 20161130")
    start_date_string = sys.argv[1]
    start_date = date(int(start_date_string[0:4]), int(start_date_string[4:6]), int(start_date_string[6:8]))
    end_date_string = sys.argv[2]
    end_date = date(int(end_date_string[0:4]), int(end_date_string[4:6]), int(end_date_string[6:8]))

    joined = []

    for current_date in common.dates_in_range(start_date, end_date):
        blob_prefix = current_date.strftime('%Y/%m/%d/') #'{0}/{1}/{2}/'.format(current_date.year, current_date.month, current_date.day)
        joined += filter(lambda b: b.properties.content_length != 0, block_blob_service.list_blobs(joined_examples_container, prefix = blob_prefix))

    joined = map(common.parse_name, joined)
    joined = list(joined)
   
    def load_data(ts, blob):
        jd = common.JoinedData(block_blob_service, cache_folder, joined_examples_container, ts, blob)
        if not os.path.exists(jd.filename + '.ids'):
            print("Indexing " + jd.filename)
            subprocess.call("C:\\work\\mwt-ds\\x64\\Release\\LogCookig.exe " + jd.filename)

    data = []
    print("Downloading & indexing events...")
    with Pool(processes = 4) as p:
        data = p.map(lambda x:load_data(x[0], x[1]), joined)

    