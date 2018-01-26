from azure.storage.blob import BlockBlobService
import configparser
from datetime import date, datetime, timedelta
import json
import re
import ntpath
import os
import os.path
import sys
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


def get_checkpoint_models(block_blob_service, start_date, end_date):
    for current_date in dates_in_range(start_date, end_date):
        for time_container in block_blob_service.list_blobs('onlinetrainer', prefix = current_date.strftime('%Y%m%d/'), delimiter = '/'):
            m = re.search('^([0-9]{4})([0-9]{2})([0-9]{2})/([0-9]{2})([0-9]{2})([0-9]{2})', time_container.name)
            if m:
                ts = datetime(int(m.group(1)), int(m.group(2)), int(m.group(3)), int(m.group(4)), int(m.group(5)), int(m.group(6)))    
                yield (ts, 'onlinetrainer', time_container.name) 

def get_online_settings(block_blob_service, cache_folder):
    online_settings_blob = CachedBlob(block_blob_service, cache_folder, 'mwt-settings', 'client')
    return json.load(open(online_settings_blob.filename, 'r', encoding='utf8'))

class CachedBlob:
    def __init__(self, block_blob_service, root, container, name, expected_size = None):
        self.filename = os.path.join(str(root), str(container), str(name))
        
        if not os.path.exists(self.filename):
            # download not existing file
            print(self.filename)
            os.makedirs(ntpath.dirname(self.filename), exist_ok=True)
            block_blob_service.get_blob_to_path(container, name, self.filename)
        else:
            # verify size matches
            if expected_size is not None:
                actual_size = os.stat(self.filename).st_size 
                if actual_size != expected_size:
                    print('{0} mismatch in size. Expected: {1} vs {2}'.format(self.filename, expected_size, actual_size))
                    os.remove(self.filename)
                    block_blob_service.get_blob_to_path(container, name, self.filename)

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
            self.file = open(self.joined_data.filename, 'r', encoding='utf8')
            
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

class Event:
    def __init__(self, ids):
        self.evt_id = ids[0]
        if len(ids) > 1:
            self.model_id = ids[1]
        else:
            self.model_id = None

# single joined data blob
class JoinedData(CachedBlob):
    def __init__(self, block_blob_service, root, joined_examples_container, ts, blob):
        super(JoinedData,self).__init__(block_blob_service, root, joined_examples_container, blob.name, blob.properties.content_length)
        self.blob = blob
        self.ts = ts
        self.ids = []
        self.data = []

    def index(self):
        if os.path.exists(self.filename + '.ids'):
            f = open(self.filename + '.ids', 'r', encoding='utf8')
            for line in f:
                event_and_model_id = line.rstrip('\n').split(' ')
                self.ids.append(Event(event_and_model_id))
            f.close()
        else:
            with open(self.filename + '.ids', 'w', encoding='utf8') as f_id:
                with open(self.filename, 'r', encoding='utf8') as f:
                    for line in f:
                        js = json.loads(line)

                        evt_id = js['_eventid']
                        _ = f_id.write(evt_id)

                        # model id might be missing in older data sets
                        model_id = None
                        if '_modelid' in js:
                            model_id = js['_modelid']
                            if model_id is not None:
                                _ = f_id.write(' ')
                                _ = f_id.write(str(model_id))
                        _ = f_id.write('\n')

                        self.ids.append(Event([evt_id, model_id]))

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

        
def line_prepender(filename, line):
    with open(filename, 'r+') as f:
        content = f.read()
        f.seek(0, 0)
        f.write(line.rstrip('\r\n') + '\n' + content)

class CheckpointedModel:
    def __init__(self, block_blob_service, ts, root, container, dir):
        self.ts = ts
        self.container = container
        
        # uncomment to download models
        self.model = CachedBlob(block_blob_service, root, container, '{0}model'.format(dir))
        self.trackback = CachedBlob(block_blob_service, root, container, '{0}model.trackback'.format(dir))
        self.trackback_ids = []

        with open(self.trackback.filename, 'r', encoding='utf8') as f:
            line = f.readline()
            m = re.search('^modelid: (.+)$', line)
            if m is not None:
                self.model_id = m.group(1)
            else:
                self.model_id = None
                self.trackback_ids.append(line.rstrip('\n'))

            self.trackback_ids.extend([line.rstrip('\n') for line in f])

class DataSet:
    @classmethod
    def fromstrings(cls, start_date_string, end_date_string):
        start_date = date(int(start_date_string[0:4]), int(start_date_string[4:6]), int(start_date_string[6:8]))
        end_date = date(int(end_date_string[0:4]), int(end_date_string[4:6]), int(end_date_string[6:8]))

        return cls(start_date, end_date)

    def __init__(self, start_date, end_date):
        self.start_date = start_date
        self.end_date = end_date
        
        self.config = configparser.ConfigParser()
        self.config.read('ds.config')
        self.ds = self.config['DecisionService']
        self.cache_folder = self.ds['CacheFolder']
        self.joined_examples_container = self.ds['JoinedExamplesContainer']
        self.experimental_unit_duration_days = self.ds['ExperimentalUnitDurationDays']
        
        # https://azure-storage.readthedocs.io/en/latest/_modules/azure/storage/blob/models.html#BlobBlock
        self.block_blob_service = BlockBlobService(connection_string=self.config['AzureStorageAuthentication']['$Default'])

        # Lookback 'experimental_unit_duration_days' for events
        self.start_date_withlookback = start_date + timedelta(days = -int(self.experimental_unit_duration_days))

        self.ordered_joined_events_filename = os.path.join(self.cache_folder, 
                                                           'data_{0}-{1}.json'.format(start_date.strftime('%Y%m%d'), end_date.strftime('%Y%m%d')))
                
        # create scoring directories for [start_date, end_date] range
        self.scoring_dir = os.path.join(self.cache_folder, 'scoring')
        os.makedirs(self.scoring_dir, exist_ok=True)

    def download_events(self):
        temp = []

        for current_date in dates_in_range(self.start_date_withlookback, self.end_date):
            blob_prefix = current_date.strftime('%Y/%m/%d/') #'{0}/{1}/{2}/'.format(current_date.year, current_date.month, current_date.day)
            temp  += filter(lambda b: b.properties.content_length != 0, self.block_blob_service.list_blobs(self.joined_examples_container, prefix = blob_prefix))

        self.joined = list(map(parse_name, temp))

        self.global_idx = {}
        self.global_model_idx = {}
        self.data = []
    
        def load_data(ts, blob):
            jd = JoinedData(self.block_blob_service, self.cache_folder, self.joined_examples_container, ts, blob)
            jd.index()
            return jd

        print("Downloading & indexing events...")
        with Pool(processes = 8) as p:
            self.data = p.map(lambda x:load_data(x[0], x[1]), self.joined)
            for jd in self.data:
                reader = jd.reader()
                for evt in jd.ids:
                    # print("'{0}' <- {1}" .format(evt.evt_id, reader))
                    self.global_idx[evt.evt_id] = reader
        
    def build_model_history(self):
        print('Found {0} events. Sorting data files by time...'.format(len(self.global_idx)))
        self.data.sort(key=lambda jd: jd.ts)

        # reproduce training, by using trackback files
        self.model_history = list(get_checkpoint_models(self.block_blob_service, self.start_date_withlookback, self.end_date))
        with Pool(5) as p:
            self.model_history = p.map(lambda x: CheckpointedModel(self.block_blob_service, x[0], self.cache_folder, x[1], x[2]), self.model_history)
            for m in self.model_history:
                if m.model_id is not None:
                    self.global_model_idx[m.model_id] = m
                
        self.model_history.sort(key=lambda jd: jd.ts)

    def get_online_settings(self):
        online_settings_blob = CachedBlob(self.block_blob_service, self.cache_folder, 'mwt-settings', 'client')
        return json.load(open(online_settings_blob.filename, 'r', encoding='utf8'))

    def create_files(self):
        for local_date in dates_in_range(self.start_date, self.end_date):
            scoring_dir_date = os.path.join(self.scoring_dir, local_date.strftime('%Y/%m/%d'))
            if os.path.exists(scoring_dir_date):
                rmtree(scoring_dir_date)
            os.makedirs(scoring_dir_date)

        ordered_joined_events = open(self.ordered_joined_events_filename, 'w', encoding='utf8')
        num_events_counter = 0
        missing_events_counter = 0

        model_history_withindaterange = filter(lambda x : x.ts.date() >= self.start_date, self.model_history)
        print('Creating {0} scoring models...'.format(len(list(model_history_withindaterange))))
    
        for m in self.model_history:
            # for scoring and ips calculations, we only consider models within [start_date, end_date]
            if m.ts.date() < self.start_date:
                continue
            
            print('Creating scoring models {0}...'.format(m.ts.strftime('%Y/%m/%d %H:%M:%S')))
            num_valid_events = 0

            if m.model_id is None:
                # no modelid available, skipping scoring event creation
                for event_id in m.trackback_ids:
                    # print("'{0}'" .format(event_id))
                    if event_id in self.global_idx:
                        # print("found '{0}'" .format(event_id))    
                        line = self.global_idx[event_id].read(event_id)
                        if line:
                            line = line.strip() + ('\n')
                            _ = ordered_joined_events.write(line)
                            num_events_counter += 1
                            num_valid_events += 1
                    else:
                        missing_events_counter += 1
            else:
                for event_id in m.trackback_ids:
                    if event_id in self.global_idx:
                        line = self.global_idx[event_id].read(event_id)
                        if line:
                            line = line.strip() + ('\n')

                            _ = ordered_joined_events.write(line)
                            num_events_counter += 1
                            num_valid_events += 1
                        
                            scoring_model_id = json.loads(line)['_model_id']
                            if scoring_model_id is None:
                                continue # this can happen at the very beginning if no model was available
                        
                            if scoring_model_id not in self.global_model_idx:
                                continue # this can happen if the event was scored using a model that lies outside our model history
                                                                        
                            scoring_model = self.global_model_idx[scoring_model_id]
                            if scoring_model.ts.date() >= self.start_date:
    #                           the event was scored using a model which was generated prior to start_date
    #                           so we can exclude it from scoring
                                scoring_filename = os.path.join(self.scoring_dir, 
                                                            scoring_model.ts.strftime('%Y'), 
                                                            scoring_model.ts.strftime('%m'), 
                                                            scoring_model.ts.strftime('%d'),
                                                            scoring_model_id + '.json')
                                                        
                                # with open(scoring_filename, 'a', encoding='utf8') as scoring_file:
                                #     _ = scoring_file.write(line)

                    else:
                        missing_events_counter += 1

                if num_valid_events > 0:
                    scoring_model_filename = os.path.join(self.scoring_dir, 
                                        m.ts.strftime('%Y'), 
                                        m.ts.strftime('%m'), 
                                        m.ts.strftime('%d'),
                                        m.model_id + '.model')
                                    
                    _ = ordered_joined_events.write(json.dumps({'_tag':'save_{0}'.format(scoring_model_filename)}) + ('\n'))

        ordered_joined_events.close()

    def train_models(self):
        model_history_prestart = list(filter(lambda x: x.ts.date() < self.start_date, self.model_history))
        model_init = max(model_history_prestart, key=lambda x: x.ts)
        model_init_name = model_init.trackback.filename.rsplit('.trackback', 1)[0]    

        print("Warm start model: '{0}'".format(model_init_name))
    
        # Download model_init (and make sure it works on windows)
        model_init_info = re.split('[/\\\\]+', model_init_name)[-4:]
        container = model_init_info[0]
        name = model_init_info[1] + '/' + model_init_info[2] + '/' + model_init_info[3]
        CachedBlob(self.block_blob_service, self.cache_folder, container, name)

        online_args = self.get_online_settings()['TrainArguments']

        vw_cmdline = 'vw ' + self.ordered_joined_events_filename + ' --json --save_resume --preserve_performance_counters -i ' + model_init_name + ' ' + online_args
        # vw_cmdline += ' --quiet'
        print(vw_cmdline)
    
        os.system(vw_cmdline)
