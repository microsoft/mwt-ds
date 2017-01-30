import sys
import configparser
import json
import re 
import os
import os.path
import common
from azure.storage.blob import BlockBlobService

if __name__ == '__main__':
    if len(sys.argv) < 4:
        print("Start and end dates are expected. Example: python {0} <joined_data> <start_model> <num_models>".format(sys.argv[0]))

    joined_data = sys.argv[1]
    start_model = sys.argv[2]
    num_models = int(sys.argv[3])

    config = configparser.ConfigParser()
    config.read('ds.config')
    ds = config['DecisionService']
    cache_folder = ds['CacheFolder']
    block_blob_service = BlockBlobService(account_name=ds['AzureBlobStorageAccountName'], account_key=ds['AzureBlobStorageAccountKey'])
    
    joined_data_index = {}

    # index joined data
    with open(joined_data, 'r', encoding='utf8') as f:
        pos = f.tell()
        line = f.readline()
        while len(line) != 0:
            evt = json.loads(line)
            if '_eventid' in evt:
                joined_data_index[evt['_eventid']] = pos
            pos = f.tell()
            line = f.readline()
             
    # find model dirs
    models = []
    start_model_dir = os.path.dirname(start_model)
    day_dir= os.path.abspath(os.path.join(start_model_dir,".."))
    model_dirs = os.listdir(day_dir)
    model_dirs.sort()
    for i in range(len(model_dirs)):
       model_dir = os.path.join(day_dir, model_dirs[i])
       if model_dir == start_model_dir:
           for j in range(i, min(i + num_models, len(model_dirs))):
               model_dir = os.path.join(day_dir, model_dirs[j])
               models.append(model_dir)
           break

    online_args = common.get_online_settings(block_blob_service, cache_folder)['TrainArguments']

    with open('empty.txt', 'w'):
        print('Creating empty data file')

    for i in range(len(models) - 1):
        m1_dir = models[i]
        m2_dir = models[i+1]

        trackback_filename = os.path.join(m2_dir, 'model.trackback')
        joined_data_per_model  = os.path.join(m2_dir, 'model.json')

        with open(joined_data, 'r', encoding='utf8') as input:
            with open(joined_data_per_model, 'w', encoding='utf8') as output:
                with open(trackback_filename, 'r', encoding='utf8') as trackback:
                    for id in trackback:
                        m = re.search('^modelid: (.+)$', id)
                        if m is not None:
                            model_id = m.group(1)
                            continue
                        id = id.rstrip('\n')
                        if id not in joined_data_index:
                            raise ValueError('Unable to find "{0}" id in joined data'.format(id))
                        input.seek(joined_data_index[id])
                        line = input.readline()
                        _ = output.write(line)

        # produce next model    
        m1_model = os.path.join(m1_dir, 'model')
        m2_model = os.path.join(m2_dir, 'model')
        m2_model_repro = os.path.join(m2_dir, 'model.repro')
        os.system('vw --quiet --json --save_resume --preserve_performance_counters --id {0} {1} -d {2} -i {3} -f {4}'.format(model_id, online_args, joined_data_per_model, m1_model, m2_model_repro))

        # produce comparable models
        os.system('vw --quiet --save_resume -i {0} -f {0}.stripped --readable_model {0}.txt -d empty.txt'.format(m2_model))
        os.system('vw --quiet --save_resume -i {0} -f {0}.stripped --readable_model {0}.txt -d empty.txt'.format(m2_model_repro))

        # run diff on model.txt and model.repro.txt 
    