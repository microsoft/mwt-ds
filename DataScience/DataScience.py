import common
import ntpath
import os
import os.path
import sys
import configparser
from azure.storage.blob import BlockBlobService
import re
import itertools
from datetime import date, datetime, timedelta
import json
from tabulate import tabulate
import time
from multiprocessing.dummy import Pool
from shutil import rmtree
from vowpalwabbit import pyvw


# m = CheckpointedModel(1, 'd:/Data/TrackRevenue', 'onlinetrainer', '20161204\\000052\\')


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
        print("Start and end dates are expected. Example: python datascience.py 20161122 20161130")
    start_date_string = sys.argv[1]
    start_date = date(int(start_date_string[0:4]), int(start_date_string[4:6]), int(start_date_string[6:8]))
    end_date_string = sys.argv[2]
    end_date = date(int(end_date_string[0:4]), int(end_date_string[4:6]), int(end_date_string[6:8]))

    # remove me
    # start_date = date(2016, 12, 3)
    # start_date_string = '20161203'
    # end_date = date(2016, 12, 4)
    # end_date_string = '20161204'

    joined = []

    for current_date in common.dates_in_range(start_date, end_date):
        blob_prefix = current_date.strftime('%Y/%m/%d/') #'{0}/{1}/{2}/'.format(current_date.year, current_date.month, current_date.day)
        joined += filter(lambda b: b.properties.content_length != 0, block_blob_service.list_blobs(joined_examples_container, prefix = blob_prefix))

    joined = map(common.parse_name, joined)
    joined = list(joined)
    
    global_idx = {}
    global_model_idx = {}
    data = []
    
    def load_data(ts, blob):
        jd = common.JoinedData(block_blob_service, cache_folder, joined_examples_container, ts, blob)
        jd.index()
        return jd

    print("Downloading & indexing events...")
    with Pool(processes = 8) as p:
        data = p.map(lambda x:load_data(x[0], x[1]), joined)
        for jd in data:
            reader = jd.reader()
            for evt in jd.ids:
                # print("'{0}' <- {1}" .format(evt.evt_id, reader))
                global_idx[evt.evt_id] = reader

    print('Found {0} events. Sorting data files by time...'.format(len(global_idx)))
    data.sort(key=lambda jd: jd.ts)

    #ordered_joined_events = open(os.path.join(cache_folder, 'data_' + start_date_string + '-' + end_date_string + '.json'), 'w', encoding='utf8')
    #for jd in data:
    #    print (jd.filename)
    #    file = open(jd.filename, 'r', encoding='utf8')
    #    for line in file:
    #        line = line.strip() + ('\n')
    #        _ = ordered_joined_events.write(line)
    #ordered_joined_events.close()

    #sys.exit(0)

    # concatenate file ordered by time

    def tabulate_metrics(metrics, top = None):
        headers = ['timestamp']
        for n in list(itertools.islice(metrics, 1))[0].names:
            headers.extend(['{0} cost'.format(n), '{0} action'.format(n)]) 
        headers.extend(['prob', 'file'])

        data = itertools.chain.from_iterable(map(lambda x : x.tabulate_data(), metrics))

        if top:
            data = itertools.islice(data, top)
        
        return tabulate(data, headers)

    # m = map(lambda d: d.metric({'constant 1': lambda x: 1, 'constant 2':lambda x: 2}), data)
    # print(tabulate_metrics(m, 10))

    # reproduce training, by using trackback files
    model_history = list(common.get_checkpoint_models(block_blob_service, start_date, end_date))
    with Pool(5) as p:
        model_history = p.map(lambda x: common.CheckpointedModel(block_blob_service, x[0], cache_folder, x[1], x[2]), model_history)
        for m in model_history:
            if m.model_id is not None:
                global_model_idx[m.model_id] = m
    model_history.sort(key=lambda jd: jd.ts)

    # create scoring directories 2016/03/12
    scoring_dir = os.path.join(cache_folder, 'scoring')
    if not os.path.exists(scoring_dir):
        os.makedirs(scoring_dir)

    local_date = start_date
    while local_date <= end_date:
        scoring_dir_date = os.path.join(scoring_dir, local_date.strftime('%Y/%m/%d'))
        if os.path.exists(scoring_dir_date):
            rmtree(scoring_dir_date)
        os.makedirs(scoring_dir_date)
        local_date += timedelta(days=1)

    ordered_joined_events = open(os.path.join(cache_folder, 'data_' + start_date_string + '-' + end_date_string + '.json'), 'w', encoding='utf8')
    num_events_counter = 0
    missing_events_counter = 0

    for m in model_history:
        # print('Processing {0}...'.format(m.ts.strftime('%Y/%m/%d %H:%M:%S')))
        num_valid_events = 0
        # TODO: skipping events that were not in the joined-examples downloaded. This misses {experiment_unit_duration_in_hours / 24} of the events.
        # Need to use events or model files from outside the date range.
        if m.model_id is None:
            # no modelid available, skipping scoring event creation
            for event_id in m.trackback_ids:
                # print("'{0}'" .format(event_id))
                if event_id in global_idx:
                    # print("found '{0}'" .format(event_id))    
                    line = global_idx[event_id].read(event_id)
                    if line:
                        line = line.strip() + ('\n')
                        _ = ordered_joined_events.write(line)
                        num_events_counter += 1
                        num_valid_events += 1
                else:
                    missing_events_counter += 1
        else:
            for event_id in m.trackback_ids:
                if event_id in global_idx:
                    line = global_idx[event_id].read(event_id)
                    if line:
                        line = line.strip() + ('\n')
                        scoring_model_id = json.loads(line)['_modelid']
                        if scoring_model_id is None:
                            continue # this can happen at the very beginning if no model was available
                        
                        scoring_model = global_model_idx[scoring_model_id]
                        _ = ordered_joined_events.write(line)

                        scoring_filename = os.path.join(scoring_dir, 
                                                        scoring_model.ts.strftime('%Y'), 
                                                        scoring_model.ts.strftime('%m'), 
                                                        scoring_model.ts.strftime('%d'),
                                                        scoring_model_id + '.json')
                        with open(scoring_filename, "a") as scoring_file:
                            _ = scoring_file.write(line)
                        num_events_counter += 1
                        num_valid_events += 1
                else:
                    missing_events_counter += 1
        if num_valid_events > 0:
            scoring_model_filename = os.path.join(scoring_dir, 
                                m.ts.strftime('%Y'), 
                                m.ts.strftime('%m'), 
                                m.ts.strftime('%d'),
                                m.modelid + '.model')
            _ = ordered_joined_events.write(json.dumps({'_tag':'save_{0}'.format(scoring_model_filename)}) + ('\n'))
    ordered_joined_events.close()

    # Commenting out debugging prints
    """
    for m in model_history:
        print('ts: {0} events: {1}'.format(m.ts, len(m.trackback_ids)))

        for event_id in m.trackback_ids:
            print(event_id)
    """

    print('Number of events downloaded: %d' % num_events_counter)
    print('Number of missing events: %d' % missing_events_counter)

    print("Time taken: %s seconds" % (time.time() - start_time))

    # iterate through model history
    # find source JoinedData
    # get json entry (read until found, cache the rest)



    # calculate offline metric for each policy and see if we get the same result

    # download data in order
    # arrange according to model.trackback files
    ## 1 training
    ## 2 evaluation

    # build index for events
