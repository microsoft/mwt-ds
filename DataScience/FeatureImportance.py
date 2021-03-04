#!/usr/bin/env python3
# ==========================================================================================
# Find feature importance
# ==========================================================================================

'''
Usage example:
>python FeatureImportance.py -d data.json --ml_args "--cb_adf -l 0.01 --cb_type mtr" -m model.vw -n 5

Sample output:
=====================================
Testing a range of L1 regularization
L1: 1e-06 - Num of Features: 4197
L1: 1e-05 - Num of Features: 2741
L1: 1e-04 - Num of Features: 46
L1: 1e-03 - Num of Features: 13
=====================================

Inverting hashes of feature importance (l1 = 1e-03)
=====================================
Emotion0^contempt:194265:-0.0236295
Emotion0^disgust:251874:-0.0327635
Emotion0^fear:188970:1.0217
Emotion0^surprise:1982:-0.416561
Emotion1^anger:197213:-0.486541
Emotion1^contempt:94653:-0.34001
Emotion1^disgust:166742:-0.368214
Emotion1^fear:23093:-5.25565
Emotion2^anger:104155:-0.20074
Emotion2^contempt:104000:-0.457823
Emotion2^disgust:22454:-0.142982
Emotion2^fear:23283:-177.874
Emotion2^sadness:157471:-1.35735
=====================================
'''

import os, argparse, sys
from subprocess import check_output, DEVNULL, Popen, TimeoutExpired
from loggers import Logger

def get_pretty_feature(feature):
    tokens = feature.split('^')
    if tokens[0] == 'FromUrl':
        tokens[0] = 'Context'
    elif tokens[0] == 'i' or tokens[0] == 'j':
        tokens[0] = 'Action'
    return '.'.join(tokens)

def get_pretty_features(features):
    featurelist = features.split('*')
    pretty_feature_list = list(map(get_pretty_feature, featurelist))
    return " with ".join(pretty_feature_list)

def extract_features(fp, inv_hash):
    features = []
    text = open(fp).read().split('\n:0\n',1)[1].strip()
    if '\n' not in text:
        Logger.info('no features found in model output file: {0}'.format(fp))
    else:
        for line in text.splitlines():
            data = line.split(':')
            if data[0] in inv_hash:
                features.append(inv_hash[data[0]])
            else:
                Logger.info('missing hash value in inv_hash: {0}'.format(data[0]))
    return features

# read the invert hash file and return a dictionary that maps from hash value to feature.
def get_feature_inv_hash(fp):
    inv_hash = {}
    text = open(fp, encoding='utf-8').read().split('\n:0\n',1)[1].strip()
    if '\n' not in text:
        Logger.info('no features found in invert hash file: {0}.'.format(fp))
    else:
        for line in text.splitlines():
            data = line.split(':')
            if (len(data) == 3):
                inv_hash[data[1]] = data[0]
    return inv_hash

# return unique buckets of features from the feature funnel.
# sample: input => [['c','b','a','d','e'],['b','c','a'],['a']] returns output => [['a'], ['b', 'c'], ['d', 'e']]
def get_feature_buckets(features_funnel):
    union_features = []
    feature_buckets = []
    for features in reversed(features_funnel):
        unique_features = list(set(features) - set(union_features))
        if len(unique_features) > 0:
            unique_features.sort()
            feature_buckets.append(unique_features)
            union_features.extend(unique_features)
    return feature_buckets

def get_feature_importance(log_file, ml_args, warmstart_model=None, min_num_features=5, invert_hash_timeout=5*3600):
    invHash_fp = log_file+'.invHash.txt'

    if ' --l1 ' in ml_args:
        temp = ml_args.split(' --l1 ',1)
        ml_args = temp[0]
        if ' ' in temp[1]:
            temp = temp[1].split(' ',1)
            ml_args += ' ' + temp[1]
            l1 = float(temp[0])
        else:
            l1 = float(temp[1])
    else:
        l1 = 1e-7

    vw_base = 'vw ' + ml_args + ' --dsjson --data {0} --quiet'.format(log_file)
    if warmstart_model:
        vw_base += ' -i {0}'.format(warmstart_model)

    print('\n=====================================')
    Logger.info('Generating invert hash file to map the hash to feature names')

    vw_inv_hash_cmd = vw_base + ' --invert_hash {0}'.format(invHash_fp)
    Logger.info('command to get invert hash file: {0}'.format(vw_inv_hash_cmd))
    invert_hash_process = Popen(vw_inv_hash_cmd.split())
    try:
        invert_hash_process.wait(invert_hash_timeout)
    except TimeoutExpired:
        Logger.info('Invert hash file generation timed out after {0} seconds.'.format(invert_hash_timeout))
        invert_hash_process.kill()
        Logger.info('Exit the computation of feature importance')
        return [[],[]]
    inv_hash = get_feature_inv_hash(invHash_fp)

    print('\n=====================================')
    Logger.info('Testing a range of L1 regularization')
    all_features_funnel = []
    index = 0
    max_run_count = 20
    while True:
        readModel_fp = log_file+'.readModel.{0}.txt'.format(index)
        vw_readable_model_cmd_base = vw_base + ' --readable_model {0}'.format(readModel_fp)
        vw_readable_model_cmd = vw_readable_model_cmd_base + ' -c --l1 {0}'.format(l1)
        index += 1
        os.system(vw_readable_model_cmd)
        features = extract_features(readModel_fp, inv_hash)
        num_features = len(features)
        Logger.info('L1: {0:.0e} - Num of Features: {1}, File - {2}'.format(l1, num_features, os.path.basename(readModel_fp)))

        all_features_funnel.append(features)

        # If we fall below the minimum number of features, then break out of the loop.
        if num_features < min_num_features:
            Logger.info('Number of features is {0} which is below the minimum of {1}. Exiting the loop with L1 value of: {2:.0e}'.format(num_features, min_num_features, l1))
            break

        # Add a max run count so we avoid getting stuck in an infinite loop for any special case.
        if index > max_run_count:
            Logger.info('Run count exceeds max run count. Exiting the loop with L1 value of: {0:.0e}'.format(l1))
            break
        else:
            l1 *= 10
    Logger.info("feature funnel sizes: {0}".format([len(features) for features in all_features_funnel]))
    feature_buckets = get_feature_buckets(all_features_funnel)
    not_required_features = ['constant', 'action.constant']
    pretty_feature_buckets = [[get_pretty_features(feature) for feature in feature_bucket] for feature_bucket in feature_buckets]
    pretty_feature_buckets = [[f for f in bucket if f.lower() not in not_required_features] for bucket in pretty_feature_buckets]
    return [feature_buckets, pretty_feature_buckets]

def add_parser_args(parser):
    parser.add_argument('-d', '--data', type=str, help="input log file.", required=True)
    parser.add_argument('--ml_args', help="ML arguments (default: --cb_adf -l 0.01)", default='--cb_adf -l 0.01')
    parser.add_argument('-m', '--model', type=str, help="VW warmstart_model.", default=None)
    parser.add_argument('-n', '--min_num_features', type=str, help="Minimum Number of features.", default='5')
    parser.add_argument('--invert_hash_timeout', type=str, help="Timeout in seconds for computing invert hash (default: 5 hours).", default='18000')

def main(args):
    try:
        check_output(['vw','-h'], stderr=DEVNULL)
    except:
        Logger.error("Vowpal Wabbit executable not found. Please install and add it to your path")
        sys.exit(1)
    return get_feature_importance(args.data, args.ml_args, args.model, int(args.min_num_features), int(args.invert_hash_timeout))

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    add_parser_args(parser)
    feature_importance = main(parser.parse_args())
    Logger.info("feature importance sizes: {0}".format([len(features) for features in feature_importance]))