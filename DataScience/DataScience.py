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

def dates_in_range(start_date, end_date):
    num_days = (end_date - start_date).days
    for i in range(num_days):
        yield start_date + timedelta(days = i)

def parse_name(blob):
    m = re.search('^([0-9]{4})/([0-9]{2})/([0-9]{2})/([0-9]{2})/(.*)\.json$', blob.name)
    dt = datetime(int(m.group(1)), int(m.group(2)), int(m.group(3)), int(m.group(4)))    
    return (dt, blob)

class CachedBlob:
    def __init__(self, root, container, name):
        self.filename = os.path.join(str(root), str(container), str(name))
        if not os.path.exists(self.filename):
            print(self.filename)
            dn = ntpath.dirname(self.filename)
            if not os.path.exists(dn):
                os.makedirs(dn)
            block_blob_service.get_blob_to_path(container, name,self. filename)

class JoinedDataReader:
    def __init__(self, joined_data):
        self.file = None
        self.joined_data = joined_data
        self.read_ahead = {}

    def read(self, eventid):
        data = self.read_ahead.pop(eventid, None)
        if data:
            return data

        if not self.file:
            self.file = open(self.joined_data.filename, 'r')
            
        # read all events in file
        ret = None
        for line in self.file:
            js = json.loads(line)
            js_event_id = js['_eventid']
            if (js_event_id == eventid):
                ret = line
            else:
                self.read_ahead[js_event_id] = line

        self.file.close()
        self.file = None

        return ret

# single joined data blob
class JoinedData(CachedBlob):
    def __init__(self, root, joined_examples_container, ts, blob):
        super(JoinedData,self).__init__(root, joined_examples_container, blob.name)
        self.blob = blob
        self.ts = ts
        self.blob = blob
        self.ids = []
        self.data = []

    def index(self, idx):
        f = open(self.filename, 'r', encoding='utf8')
        reader = self.reader()
        for line in f:
            js = json.loads(line)
            evt_id = js['_eventid']
            self.ids.append(evt_id)
            idx[evt_id] = reader
        f.close()

    def ips(self, policies):
        f = open(self.filename, 'r')
        for line in f:
            js = json.loads(line)

            # TODO: probability of drop
            cost = float(js['_label_cost'])
            prob = float(js['_label_probability'])
            action_observed = int(js['_label_action'])

            # new [] { cost} .Union(map())
            estimates = [[cost, action_observed]] # include "observed" reward
            for p in policies:
                action_of_policy = policies[p](js)
                ips = cost / prob if action_of_policy == action_observed else 0
                estimates.append([ips, action_of_policy])
                        
            yield {'timestamp': js['_timestamp'], 'estimates':estimates, 'prob': prob}
        f.close()

    def metric(self, policies):
        names = ['observed']
        names.extend([key for key in policies])
        return Metric(self, names, self.ips(policies))

    def json(self):
        f = open(self.filename, 'r')
        for line in f:
            yield json.loads(line)
        f.close()

    def reader(self):
        return JoinedDataReader(self)

class Trackback(CachedBlob):
    def __init__(self, ts, root, blob):
        super(Trackback,self).__init__(root, 'onlinetrainer', blob)
        self.ts = ts
        self.blob = blob
        self.ids = []
        self.data = []
        
class CheckpointedModel:
    def __init__(self, ts, root, container, dir):
        self.ts = ts
        self.container = container
        # self.model = CachedBlob(root, container, '{0}model'.format(dir))
        self.trackback = CachedBlob(root, container, '{0}model.trackback'.format(dir))
        with open(self.trackback.filename, 'r') as f:
            line = f.readline()
            m = re.search('^modelid: (.+)$', line)
            self.modelid = m.group(1)
            self.trackback_ids = [line.rstrip('\n') for line in f]

# m = CheckpointedModel(1, 'd:/Data/TrackRevenue', 'onlinetrainer', '20161204\\000052\\')

def get_checkpoint_models():
    for current_date in dates_in_range(start_date, end_date):
        for time_container in block_blob_service.list_blobs('onlinetrainer', prefix = current_date.strftime('%Y%m%d/'), delimiter = '/'):
            m = re.search('^([0-9]{4})([0-9]{2})([0-9]{2})/([0-9]{2})([0-9]{2})([0-9]{2})', time_container.name)
            if m:
                ts = datetime(int(m.group(1)), int(m.group(2)), int(m.group(3)), int(m.group(4)), int(m.group(5)), int(m.group(6)))    
                yield (ts, 'onlinetrainer', time_container.name)

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

    for current_date in dates_in_range(start_date, end_date):
        blob_prefix = current_date.strftime('%Y/%m/%d/') #'{0}/{1}/{2}/'.format(current_date.year, current_date.month, current_date.day)
        joined += filter(lambda b: b.properties.content_length != 0, block_blob_service.list_blobs(joined_examples_container, prefix = blob_prefix))

    joined = map(parse_name, joined)
    joined = list(joined)
    
    global_idx = {}
    global_model_idx = {}
    data = []
    
    with Pool(processes = 5) as p:
        data = p.map(lambda x:JoinedData(cache_folder, joined_examples_container, x[0], x[1]), joined)
        for jd in data:
            jd.index(global_idx)

    data.sort(key=lambda jd: jd.ts)

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
    model_history = list(get_checkpoint_models())
    with Pool(5) as p:
        model_history = p.map(lambda x: CheckpointedModel(x[0], cache_folder, x[1], x[2]), model_history)
        for m in model_history:
            global_model_idx[m.modelid] = m
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

    ordered_joined_events = open(os.path.join(cache_folder, 'data_' + start_date_string + '-' + end_date_string + '.json'), 'w')
    num_events_counter = 0
    missing_events_counter = 0

    for m in model_history:
        for event_id in m.trackback_ids:
            # TODO: skipping events that were not in the joined-examples downloaded. This misses {experiment_unit_duration_in_hours / 24} of the events.
            # Need to use events or model files from outside the date range.
            if event_id in global_idx:
                line = global_idx[event_id].read(event_id)
                if line:
                    line = line.strip() + ('\n')
                    scoring_model_id = json.loads(line)['_modelid']
                    if scoring_model_id is None:
                        continue # this can happen at the very beginning if no model was available
                        
                    scoring_model = global_model_idx[scoring_model_id]
                    _ = ordered_joined_events.write(line)

                    scoring_filename = os.path.join(scoring_dir, scoring_model.ts.strftime('%Y/%m/%d'), scoring_model_id)
                    with open(scoring_filename, "a") as scoring_file:
                        _ = scoring_file.write(line)
                    num_events_counter += 1
            else:
                missing_events_counter += 1
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
