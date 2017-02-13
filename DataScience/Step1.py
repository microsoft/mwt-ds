import ntpath
import os
import os.path
import sys
import configparser
from azure.storage.blob import BlockBlobService
import re
import itertools
import time
import sys
import common

if __name__ == '__main__':

    start_time = time.time()

    # Parse start and end dates for getting data
    if len(sys.argv) < 3:
        print("Start and end dates are expected. Example: python datascience.py 20161122 20161130")

    data_set = common.DataSet.fromstrings(sys.argv[1], sys.argv[2])

    data_set.download_events()
    data_set.build_model_history()  

    data_set.create_files()
    