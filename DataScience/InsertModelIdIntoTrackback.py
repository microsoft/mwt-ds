import os
import os.path
from vowpalwabbit import pyvw
import configparser

def line_prepender(filename, line):
    with open(filename, 'r+') as f:
        content = f.read()
        f.seek(0, 0)
        f.write(line.rstrip('\r\n') + '\n' + content)

config = configparser.ConfigParser()
config.read('ds.config')
ds = config['DecisionService']
cache_folder = ds['CacheFolder']

for root, subdirs, files in os.walk(os.path.join(cache_folder, 'onlinetrainer')):
    print('looking at folder {0}'.format(root))
    model = None
    trackback = None
    for file in files:
        if file == 'model':
            model = os.path.join(root, file)
            continue
        if file == 'model.trackback':
            trackback = os.path.join(root, file)
            continue
    
    if model is None or trackback is None:
        continue
    
    print('looking at folder {0}'.format(root))

    with open(trackback, 'r') as f:
        first_line = f.readline()
        if (first_line.startswith('modelid:')):
            continue

    vw = pyvw.vw("--quiet -i {0}".format(model))
    id = vw.id()
    del vw

    line_prepender(trackback, 'modelid: {0}\n'.format(id))
