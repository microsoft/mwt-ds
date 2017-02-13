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

    # Parse start and end dates for getting data
    if len(sys.argv) < 3:
        print("Start and end dates are expected. Example: python datascience.py 20161122 20161130")
    
    data_set = common.DataSet.fromstrings(sys.argv[1], sys.argv[2])

    data_set.download_events()
    data_set.build_model_history()  

    data_set.train_models()

    online_args = data_set.get_online_settings()['TrainArguments']
    print('Online VW arguments: ' + online_args)

    vw_args = online_args.split(' ')
    epsilon = float(vw_args[vw_args.index("--epsilon") + 1])
   
    replayinfo_filename = os.path.join(data_set.cache_folder,
                                       'replayinfo_{0}-{1}.log'.format(data_set.start_date.strftime('%Y%m%d'), data_set.end_date.strftime('%Y%m%d')))

    print("Scoring...")
    with open(replayinfo_filename, 'w') as replayinfo_file:
        line = '\t'.join(['_eventid', '_label_action', '_label_cost', '_label_probability', 'onlineDistr', 'offlineDistr']) + '\n'
        replayinfo_file.write(line)
        
        replayScoredEventsCounts = 0        
        replayEventsMatchedCounts = 0
        costSum = 0
        probSum = 0
        
        for m in data_set.model_history:
            # for scoring and ips calculations, we only consider models within [start_date, end_date]
            if m.ts.date() < data_set.start_date:
                continue

            _model_file = os.path.join(data_set.scoring_dir, 
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
                    
                    onlineMap = dict(map(lambda x: [x[0], float(x[1])], [ap.split(':') for ap in onlineActionProbRankings.split(',')]))
                    offlineMap = dict(map(lambda x: [x[0], float(x[1])], [ap.split(':') for ap in offlineActionProbRankings.split(',')]))

                    # heres the corresponding online model

                    same = True
                    for k in onlineMap:
                        if abs(onlineMap[k] - offlineMap[k]) > 1e-5:
                            print ('mismatch {0}:{1} != {0}:{2} in event {3}'.format(k, onlineMap[k], offlineMap[k], _eventid))
                            same = False

                    replayScoredEventsCounts += 1
                    if same:
                        replayEventsMatchedCounts += 1
                    else:
                        print (onlineActionRankings)
                        print (offlineActionRankings)
                        print (onlineActionProbRankings)
                        print (offlineActionProbRankings)
                        print(vw_cmdline)
                        print ('model online: ' + data_set.global_model_idx[js['_modelid']].model.filename)

                        sys.exit(0)
                     
#                   Compute cost and prob for IPS scoring  
                    progressiveProbabilties = progressiveProbabilties = [p for a, p in sorted(list(zip(js['_a'], js['_p'])), key=lambda ap:ap[0])]
                    pi_a_x = progressiveProbabilties[_label_action - 1]
                    p_a_x = _label_probability * (1 - _probabilityofdrop)
                    cost = (_label_cost * pi_a_x) / p_a_x
                    prob = pi_a_x / p_a_x
                    
                    costSum += cost
                    probSum += prob
      
    # only available when file created              
    # print('Number of events downloaded within date range: %d' % num_events_counter)
    # print('Number of missing events: %d' % missing_events_counter)                    
    print('Number of events replayed : {0}'.format(replayScoredEventsCounts))
    
    if replayEventsMatchedCounts > 0:
        print("% of matches in data between Online DS and Offline Replay : {0} %".format(round((replayEventsMatchedCounts * 100.0) / replayScoredEventsCounts, 2)))
        
    if probSum > 0:
        print("IPS of Offline Replay events : {0}".format((costSum * 1.0) / probSum))

    print("Time taken: %s seconds" % (time.time() - start_time))
