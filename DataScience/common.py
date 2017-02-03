from azure.storage.blob import BlockBlobService
from datetime import date, datetime, timedelta
import json
import re
import ntpath
import os
import os.path
import sys

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

class CachedBlob:
    def __init__(self, block_blob_service, root, container, name, expected_size = None):
        self.filename = os.path.join(str(root), str(container), str(name))
        
        if not os.path.exists(self.filename):
            # download not existing file
            print(self.filename)
            dn = ntpath.dirname(self.filename)
            if not os.path.exists(dn):
                os.makedirs(dn)
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
        # self.model = CachedBlob(block_blob_service, root, container, '{0}model'.format(dir))
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


def get_online_settings(block_blob_service, cache_folder):
    online_settings_blob = CachedBlob(block_blob_service, cache_folder, 'mwt-settings', 'client')
    return json.load(open(online_settings_blob.filename, 'r', encoding='utf8'))