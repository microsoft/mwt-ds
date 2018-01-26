import ntpath
import os
import os.path
import configparser
from azure.storage.blob import BlockBlobService
import re
import itertools
from datetime import date
from datetime import datetime
import json
from tabulate import tabulate

config = configparser.ConfigParser()
config.read('ds.config')

ds = config['DecisionService']

# https://azure-storage.readthedocs.io/en/latest/_modules/azure/storage/blob/models.html#BlobBlock
block_blob_service = BlockBlobService(connection_string=self.config['AzureStorageAuthentication']['$Default'])

def parse_name(blob):
    m = re.search('^([0-9]{4})/([0-9]{2})/([0-9]{2})/([0-9]{2})/(.*)\.json$', blob.name)
    dt = datetime(int(m.group(1)), int(m.group(2)), int(m.group(3)), int(m.group(4)))    
    return (dt, blob)



# download data and write to CSV
eval_blobs = list(
        map(parse_name, 
            filter(lambda b: b.properties.content_length != 0, 
                block_blob_service.list_blobs('mwt-offline-eval'))))

eval_blobs.sort(key=lambda tup: tup[0])

print("Found {0} blobs".format(len(eval_blobs)))

# for blob in itertools.islice(generator, 5):
#     print(blob.name)
eval_out = open('c:\\temp\\eval.csv', 'w')
eval_out.write('"Name","AvgCost","time","window"\n')
for blob in eval_blobs:
    print(blob[1].name)
    fn = 'c:\\temp\\testdrive\\eval\\{0}'.format(blob[1].name)
    if not os.path.exists(fn):
        os.makedirs(ntpath.dirname(fn), exist_ok=True)
        block_blob_service.get_blob_to_path('mwt-offline-eval', blob[1].name, fn)
    
    f = open(fn, 'r')
    for line in f:
        if len(line) <= 1:
            continue;
        js = json.loads(line)
        _ = eval_out.write("\"{0}\",{1},{2},{3}\n".format(js['name'], js['averagecost'], js['lastenqueuedtime'], js['window']))
    f.close()
eval_out.close()


# download joined data
class Metric:
    def __init__(self, joined_data, names, estimates):
        self.joined_data = joined_data
        self.names = names
        self.estimates = estimates

    def tabulate_data(self):
        for d in self.estimates:
            l = [d['timestamp']]
            # pairs of cost/action
            for e in d['estimates']:
                l.extend(e)
            l.append(d['prob'])
            l.append(self.joined_data.blob.name)
            yield l

    def tabulate(self):
        headers = ['timestamp']
        for n in self.names:
            headers.extend(['{0} cost'.format(n), '{0} action'.format(n)]) 
        headers.extend(['prob', 'file'])

        return tabulate(self.tabulate_data(), headers)


def tabulate_metrics(metrics, top = None):
    headers = ['timestamp']
    for n in list(itertools.islice(metrics, 1))[0].names:
        headers.extend(['{0} cost'.format(n), '{0} action'.format(n)]) 
    headers.extend(['prob', 'file'])

    data = itertools.chain.from_iterable(map(lambda x : x.tabulate_data(), metrics))

    if top:
        data = itertools.islice(data, top)
    
    return tabulate(data, headers)

m = map(lambda d: d.metric({'constant 1': lambda x: 1, 'constant 2':lambda x: 2}), data)
print(tabulate_metrics(m, 10))