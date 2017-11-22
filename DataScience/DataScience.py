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
import gzip
import sys
import common

if __name__ == '__main__':

    start_time = time.time()

    config = configparser.ConfigParser()
    config.read('ds.config')
    ds = config['DecisionService']
    cache_folder = ds['CacheFolder']
    joined_examples_container = ds['JoinedExamplesContainer']
    experimental_unit_duration_days = ds['ExperimentalUnitDurationDays']

    # https://azure-storage.readthedocs.io/en/latest/_modules/azure/storage/blob/models.html#BlobBlock
    block_blob_service = BlockBlobService(connection_string=self.config['AzureStorageAuthentication']['$Default'])

    # Parse start and end dates for getting data
    if len(sys.argv) < 3:
        print("Start and end dates are expected. Example: python datascience.py 20161122 20161130")
    start_date_string = sys.argv[1]
    start_date = date(int(start_date_string[0:4]), int(start_date_string[4:6]), int(start_date_string[6:8]))
    
    # Lookback 'experimental_unit_duration_days' for events
    start_date_withlookback = start_date + timedelta(days = -int(experimental_unit_duration_days))
    
    end_date_string = sys.argv[2]
    end_date = date(int(end_date_string[0:4]), int(end_date_string[4:6]), int(end_date_string[6:8]))

    joined = []

    for current_date in common.dates_in_range(start_date_withlookback, end_date):
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

    def tabulate_metrics(metrics, top = None):
        headers = ['timestamp']
        for n in list(itertools.islice(metrics, 1))[0].names:
            headers.extend(['{0} cost'.format(n), '{0} action'.format(n)]) 
        headers.extend(['prob', 'file'])

        data = itertools.chain.from_iterable(map(lambda x : x.tabulate_data(), metrics))

        if top:
            data = itertools.islice(data, top)
        
        return tabulate(data, headers)

    # reproduce training, by using trackback files
    model_history = list(common.get_checkpoint_models(block_blob_service, start_date_withlookback, end_date))
    with Pool(5) as p:
        model_history = p.map(lambda x: common.CheckpointedModel(block_blob_service, x[0], cache_folder, x[1], x[2]), model_history)
        for m in model_history:
            if m.model_id is not None:
                global_model_idx[m.model_id] = m
                
    model_history.sort(key=lambda jd: jd.ts)

    # create scoring directories for [start_date, end_date] range
    scoring_dir = os.path.join(cache_folder, 'scoring')
    if not os.path.exists(scoring_dir):
        os.makedirs(scoring_dir)

    for local_date in common.dates_in_range(start_date, end_date):
        scoring_dir_date = os.path.join(scoring_dir, local_date.strftime('%Y/%m/%d'))
        if os.path.exists(scoring_dir_date):
            rmtree(scoring_dir_date)
        os.makedirs(scoring_dir_date)

    ordered_joined_events_filename = os.path.join(cache_folder, 'data_' + start_date_string + '-' + end_date_string + '.json')
    ordered_joined_events = open(ordered_joined_events_filename, 'w', encoding='utf8')
    num_events_counter = 0
    missing_events_counter = 0

    model_history_withindaterange = filter(lambda x : x.ts.date() >= start_date, model_history)
    print('Creating {0} scoring models...'.format(len(list(model_history_withindaterange))))
    
    for m in model_history:
        # for scoring and ips calculations, we only consider models within [start_date, end_date]
        if m.ts.date() < start_date:
            continue
            
        print('Creating scoring models {0}...'.format(m.ts.strftime('%Y/%m/%d %H:%M:%S')))
        num_valid_events = 0

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

                        _ = ordered_joined_events.write(line)
                        num_events_counter += 1
                        num_valid_events += 1
                        
                        scoring_model_id = json.loads(line)['_model_id']
                        if scoring_model_id is None:
                            continue # this can happen at the very beginning if no model was available
                        
                        if scoring_model_id not in global_model_idx:
                            continue # this can happen if the event was scored using a model that lies outside our model history
                                                                        
                        scoring_model = global_model_idx[scoring_model_id]
                        if scoring_model.ts.date() >= start_date:
#                           the event was scored using a model which was generated prior to start_date
#                           so we can exclude it from scoring
                            scoring_filename = os.path.join(scoring_dir, 
                                                        scoring_model.ts.strftime('%Y'), 
                                                        scoring_model.ts.strftime('%m'), 
                                                        scoring_model.ts.strftime('%d'),
                                                        scoring_model_id + '.json')
                                                        
                            with open(scoring_filename, "a", encoding='utf8') as scoring_file:
                                _ = scoring_file.write(line)

                else:
                    missing_events_counter += 1

            if num_valid_events > 0:
                scoring_model_filename = os.path.join(scoring_dir, 
                                    m.ts.strftime('%Y'), 
                                    m.ts.strftime('%m'), 
                                    m.ts.strftime('%d'),
                                    m.model_id + '.model')
                                    
                _ = ordered_joined_events.write(json.dumps({'_tag':'save_{0}'.format(scoring_model_filename)}) + ('\n'))

    ordered_joined_events.close()

    online_args = common.get_online_settings(block_blob_service, cache_folder)['TrainArguments']
    print('Online VW arguments: ' + online_args)

    model_history_prestart = list(filter(lambda x: x.ts.date() < start_date, model_history))
    model_init = max(model_history_prestart, key=lambda x: x.ts)
    model_init_name = model_init.trackback.filename.rsplit('.trackback', 1)[0]    

    print('Warm start model : {0}'.format(model_init_name))

#    Fix for Windows Paths.    
    model_init_name = model_init_name.replace('\\','/')
    print('...' + model_init_name)

#    Download model_init
    model_init_info = model_init_name.split('/')
    root = model_init_info[0]
    container = model_init_info[1]
    name = model_init_info[2] + '/' + model_init_info[3] + '/' + model_init_info[4]
    common.CachedBlob(block_blob_service, root, container, name)
        
    vw_cmdline = 'vw ' + ordered_joined_events_filename + ' --json --save_resume -i ' + model_init_name + ' ' + online_args
    vw_cmdline += ' --quiet'
    print(vw_cmdline)
    
    vw_args = vw_cmdline.split(' ')
    epsilon = float(vw_args[vw_args.index("--epsilon") + 1])
    os.system(vw_cmdline)
   
    replayinfo_filename = os.path.join(cache_folder, 'replayinfo_' + start_date_string + '-' + end_date_string + '.log')
    
    print("Scoring...")
    with open(replayinfo_filename, 'w') as replayinfo_file:
        line = '\t'.join(['_eventid', '_label_action', '_label_cost', '_label_probability', 'onlineDistr', 'offlineDistr']) + '\n'
        replayinfo_file.write(line)
        
        replayScoredEventsCounts = 0        
        replayExplorationEventsCounts = 0
        replayExploitationEventsCounts = 0
        replayExploitationEventsMatchedCounts = 0
        costSum = 0
        probSum = 0
        
        for m in model_history:
            # for scoring and ips calculations, we only consider models within [start_date, end_date]
            if m.ts.date() < start_date:
                continue

            _model_file = os.path.join(scoring_dir, 
                        m.ts.strftime('%Y'), 
                        m.ts.strftime('%m'), 
                        m.ts.strftime('%d'),
                        m.model_id + '.model')
                        
            _observations_file  = _model_file.replace(".model", ".json")
            _predictions_file   = _model_file.replace(".model", ".predictions")
    
            if not os.path.exists(_model_file) or not os.path.exists(_observations_file):
                continue
            
    #       Create predictions file        
    #       vw 0018aeb8-46be-4252-88ca-de87877df6f5.json --json -t -i 0018aeb8-46be-4252-88ca-de87877df6f5.model -p 0018aeb8-46be-4252-88ca-de87877df6f5.predictions
    
            vw_cmdline = 'vw ' + _observations_file + ' --json -t -i ' + _model_file + ' -p ' + _predictions_file
            vw_cmdline += ' --quiet'
            os.system(vw_cmdline)
      
            with open(_observations_file, 'r') as f_obs, \
                open(_predictions_file, 'r') as f_pred:
    
                observations  = list(filter(lambda x: x.strip(), f_obs.readlines()))
                predictions   = list(filter(lambda x: x.strip(), f_pred.readlines()))
    
                # validate number of non empty lines is same between observation and scoring
                if len(observations) != len(predictions):
                    raise ValueError('Invalid number of lines between [Observation File] and [Predictions File].')
                    
                for obs, prediction in zip(observations, predictions):
                    js = json.loads(obs)
                    _eventid            = js['_eventid']
                    _label_action       = js['_label_action'] 
                    _label_cost         = js['_label_cost'] 
                    _label_probability  = js['_label_probability']
                    _a                  = js['_a']
                    _p                  = js['_p']
                    _probabilityofdrop  = 0 if js['_probabilityofdrop'] is None else js['_probabilityofdrop']
                    
#                   Doing (a-1) here because DS action lists are 1-based indexes
                    onlineActionProbRankings         = ','.join([str(a - 1) + ":" + str(p) for a, p in zip(js['_a'], js['_p'])])
                    offlineActionProbRankings        = prediction.strip()
                    
                    onlineActionRankings    = [ap.split(':')[0] for ap in onlineActionProbRankings.split(',')]
                    offlineActionRankings   = [ap.split(':')[0] for ap in offlineActionProbRankings.split(',')]

                    line = '\t'.join(str(e) for e in [_eventid, _label_action, _label_cost, _label_probability, onlineActionRankings, offlineActionRankings]) + '\n'
                    replayinfo_file.write(line)
                                        
                    replayScoredEventsCounts += 1
                    if _label_probability <= epsilon:
#                        exploration data, exclude from matching
                        replayExplorationEventsCounts += 1
                    else:
#                        exploitation data. verify offline predictions match
                        replayExploitationEventsCounts += 1                        
                        if onlineActionRankings == offlineActionRankings:
                            replayExploitationEventsMatchedCounts += 1
                     
#                   Compute cost and prob for IPS scoring  
                    progressiveProbabilties = progressiveProbabilties = [p for a, p in sorted(list(zip(js['_a'], js['_p'])), key=lambda ap:ap[0])]
                    pi_a_x = progressiveProbabilties[_label_action - 1]
                    p_a_x = _label_probability * (1 - _probabilityofdrop)
                    cost = (_label_cost * pi_a_x) / p_a_x
                    prob = pi_a_x / p_a_x
                    
                    costSum += cost
                    probSum += prob
                    
    print('Number of events downloaded within date range: %d' % num_events_counter)
    print('Number of missing events: %d' % missing_events_counter)                    
    print('Number of events replayed : {0}'.format(replayScoredEventsCounts))
    
    if replayScoredEventsCounts > 0:
        print('Number of events in exploration : {0}.  {1} % of data'.format(replayExplorationEventsCounts, round((replayExplorationEventsCounts * 100.0) / replayScoredEventsCounts, 2)))
        print('Number of events in exploitation : {0}. {1} % of data'.format(replayExploitationEventsCounts, round((replayExploitationEventsCounts * 100.0) / replayScoredEventsCounts, 2)))
    
    if replayExploitationEventsCounts > 0:
        print("% of matches in exploitation data between Online DS and Offline Replay : {0} %".format(round((replayExploitationEventsMatchedCounts * 100.0) / replayExploitationEventsCounts, 2)))
        
    if probSum > 0:
        print("IPS of Offline Replay events : {0}".format((costSum * 1.0) / probSum))

    print("Time taken: %s seconds" % (time.time() - start_time))
