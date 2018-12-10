import pandas,ds_parse,json,collections,os,gzip,sys
import numpy as np
import argparse
import time


def get_ts_5min_bin(ts):
    str_5min = str(ts[:14],'utf-8')
    x = int(float(ts[14:16])/5)*5
    if x < 10:
        str_5min += '0'
    str_5min += str(x)+':00Z'
    return str_5min
    
def get_prediction_prob(a0, pred_line):
    # parse probability of predicted action
    # this function assume that a0 is 0-index
    
    if ':' in pred_line:                           # prediction file has pdf of all actions (as in --cb_explore_adf -p)
        if ',' in pred_line:
            if pred_line.startswith(str(a0)+':'):
                sep = ':'
            else:
                sep = ','+str(a0)+':'
            pred_prob = float(ds_parse.extract_field(pred_line,sep,','))
        else:
            if a0 == 0:
                pred_prob = 1
            else:
                print('Error: Prediction action (0) does not match log file action ({}) - log: {} - pred: {}'.format(a0,pred_line))
                sys.exit()
    else:                                          # prediction file has only one action (as in --cb_adf -p)
        pred_prob = 1 if a0 == int(pred_line) else 0

    return pred_prob

def output_dashboard_data(d, dashboard_file):
    data_dict = collections.OrderedDict()
    for x in d:
        for type in d[x]:
            for field in d[x][type]:
                data_dict.setdefault(type+'_'+field, []).append(d[x][type][field])

    df = pandas.DataFrame(data_dict, index=pandas.to_datetime([x for x in d]), dtype=float)

    df_col = collections.OrderedDict()
    for x in df.columns:
        temp = x.split('_')
        df_col.setdefault(temp[0],[]).append(temp[1])

    agg_windows = [('5T',5),('H',60),('6H',360),('D',1440)]
    with open(dashboard_file, 'w') as f:
        for ag in agg_windows:
            for index, row in df.resample(ag[0]).agg({type+'_'+field : max if field == 'c' else sum for type in df_col for field in df_col[type]}).replace(np.nan, 0.0).iterrows():
                d = []
                for type in df_col:
                    temp = collections.OrderedDict({field : row[type+'_'+field] for field in df_col[type]})
                    temp["w"] = ag[1]
                    temp["t"] = type
                    d.append(temp)
                f.write(json.dumps({"ts":index.strftime("%Y-%m-%dT%H:%M:%SZ"),"d":d})+'\n')

        # total aggregates
        tot = df.agg({type+'_'+field : max if field == 'c' else sum for type in df_col for field in df_col[type]}).replace(np.nan, 0.0)
        d = []
        for type in df_col:
            temp = collections.OrderedDict({field : tot[type+'_'+field] for field in df_col[type]})
            temp["w"] = "tot"
            temp["t"] = type
            d.append(temp)
        f.write(json.dumps({"ts":"Total","d":d})+'\n')

def merge_and_unique_stats(stats_files, dashboard_file):
    d = {}
    for stats_file in ds_parse.input_files_to_fp_list(stats_files):
        print('Processing: {}'.format(stats_file))
        for x in open(stats_file, 'rb'):
            js = json.loads(x)
            if js['d'][0]['w'] != 5:
                continue
            num = -1
            den = -1
            for y in js['d']:
                if y['t'] == 'online':
                    num = y['n']
                    den = y['d']
                    break
            if min(num, den) < 0:
                print('Error: "online" policy stats not found in input:',x)
                continue
            if js['ts'] not in d or d[js['ts']]['online']['d'] < den or (d[js['ts']]['online']['d'] == den and d[js['ts']]['online']['n'] < num):
                d[js['ts']] = {y['t'] : {field : y[field] for field in y if field not in {'w','t'}} for y in js['d']}

    print('Output dashboard data...')
    output_dashboard_data(d, dashboard_file)

def create_stats(log_fp, dashboard_file, predictions_files=None):

    t0 = time.time()

    if predictions_files is None:
        print('Searching prediction files for log file: {}'.format(log_fp))
        predictions_files = []
        for fn in os.scandir(os.path.dirname(log_fp)):
            if fn.path.startswith(log_fp+'.') and fn.name.endswith('.pred'):
                predictions_files.append(fn.path)

    # load predictions from predictions_files
    pred = {}
    for pred_fp in predictions_files:
        if os.path.isfile(pred_fp):
            name = pred_fp.split('.')[-2]   # check that policy name is encoded in file_name
            if name:
                pred[name] = [x.strip() for x in open(pred_fp) if x.strip()]
                print('Loaded {} predictions from {}'.format(len(pred[name]),pred_fp))
            else:
                print('Name is not valid - Skip: {}'.format(pred_fp))
        else:
            print('Error loading policy predictions. Pred file not found: {}'.format(pred_fp))
            sys.exit()

    if len(pred) > 1 and min(len(pred[name]) for name in pred) != max(len(pred[name]) for name in pred):
        print('Error: Prediction file length ({}) must be equal for all files'.format([len(pred[name]) for name in pred]))
        sys.exit()

    d = {}
    print('Processing: {}'.format(log_fp))
    bytes_count = 0
    tot_bytes = os.path.getsize(log_fp)
    evts = 0
    i = 0
    for x in (gzip.open(log_fp, 'rb') if log_fp.endswith('.gz') else open(log_fp, 'rb')):
        # display progress
        bytes_count += len(x)
        i += 1
        if i % 5000 == 0:
            if log_fp.endswith('.gz'):
                if i % 20000 == 0:
                    print('.', end='', flush=True)
                    if i % 1000000 == 0:
                        print(' - Iter:',i)
            else:
                ds_parse.update_progress(bytes_count,tot_bytes)

        if x.startswith(b'{"_label_cost":'):
            data = ds_parse.json_cooked(x)

            if data['skipLearn']:
                continue

            r = 0 if data['cost'] == b'0' else -float(data['cost'])

            ############################### Aggregates for each bin ######################################
            #
            # 'n':   IPS of numerator
            # 'N':   total number of samples in bin from log (IPS = n/N)
            # 'd':   IPS of denominator (SNIPS = n/d)
            # 'Ne':  number of samples in bin when off-policy agrees with log policy
            # 'c':   max abs. value of numerator's items (needed for Clopper-Pearson confidence intervals)
            # 'SoS': sum of squares of numerator's items (needed for Gaussian confidence intervals)
            #
            #################################################################################################

            # binning timestamp every 5 min
            ts_bin = get_ts_5min_bin(data['ts'])

            # initialize aggregates for ts_bin
            if ts_bin not in d:
                d[ts_bin] = collections.OrderedDict({'online' : {'n':0,'N':0,'d':0},
                                                     'baseline1' : {'n':0.,'N':0,'d':0.,'Ne':0,'c':0.,'SoS':0},
                                                     'baselineRand' : {'n':0.,'N':0,'d':0.,'Ne':0,'c':0.,'SoS':0}})
                for name in pred:
                    d[ts_bin][name] = {'n':0.,'N':0,'d':0.,'Ne':0,'c':0.,'SoS':0}

            # update aggregates for online and baseline policies
            d[ts_bin]['online']['d'] += 1
            d[ts_bin]['online']['N'] += 1
            d[ts_bin]['baselineRand']['N'] += 1
            d[ts_bin]['baseline1']['N'] += 1

            d[ts_bin]['baselineRand']['Ne'] += 1
            d[ts_bin]['baselineRand']['d'] += 1/data['p']/data['num_a']
            if data['a'] == 1:
                d[ts_bin]['baseline1']['Ne'] += 1
                d[ts_bin]['baseline1']['d'] += 1/data['p']

            if r != 0:
                d[ts_bin]['online']['n'] += r
                d[ts_bin]['baselineRand']['n'] += r/data['p']/data['num_a']
                d[ts_bin]['baselineRand']['c'] = max(d[ts_bin]['baselineRand']['c'], r/data['p']/data['num_a'])
                d[ts_bin]['baselineRand']['SoS'] += (r/data['p']/data['num_a'])**2
                if data['a'] == 1:
                    d[ts_bin]['baseline1']['n'] += r/data['p']
                    d[ts_bin]['baseline1']['c'] = max(d[ts_bin]['baseline1']['c'], r/data['p'])
                    d[ts_bin]['baseline1']['SoS'] += (r/data['p'])**2                   

            # update aggregates for additional policies from predictions
            for name in pred:
                pred_prob = get_prediction_prob(data['a']-1, pred[name][evts])     # a-1: 0-index action
                d[ts_bin][name]['N'] += 1
                if pred_prob > 0:
                    p_over_p = pred_prob/data['p']
                    d[ts_bin][name]['d'] += p_over_p
                    d[ts_bin][name]['Ne'] += 1
                    if r != 0:
                        d[ts_bin][name]['n'] += r*p_over_p
                        d[ts_bin][name]['c'] = max(d[ts_bin][name]['c'], r*p_over_p)
                        d[ts_bin][name]['SoS'] += (r*p_over_p)**2
            evts += 1
    if not log_fp.endswith('.gz'):
        len_text = ds_parse.update_progress(bytes_count,tot_bytes)
        sys.stdout.write("\r" + " "*len_text + "\r")
        sys.stdout.flush()

    print('Processed {} events'.format(evts))
    if any(len(pred[name]) != evts for name in pred):
        print('Error: Prediction file length ({}) is different from number of events in log file ({})'.format([len(pred[name]) for name in pred],evts))
        sys.exit()

    output_dashboard_data(d, dashboard_file)
    
    print('Total Elapsed Time: {:.1f} sec.'.format(time.time()-t0))


if __name__ == '__main__':

    parser = argparse.ArgumentParser()
    parser.add_argument('-l','--log_fp', help="data file path (.json or .json.gz format - each line is a dsjson)", required=True)
    parser.add_argument('-o','--output_fp', help="output file", required=True)

    args_dict = vars(parser.parse_args())   # this creates a dictionary with all input CLI
    for x in args_dict:
        locals()[x] = args_dict[x]  # this is equivalent to foo = args.foo

    create_stats(log_fp, output_fp)
