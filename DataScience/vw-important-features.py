#!/usr/bin/env python3
# ==========================================================================================
# Find important features
# ==========================================================================================

'''
Usage example:
>vw-important-features -d data.json --ml_args "--cb_adf -l 0.01 --cb_type mtr"

Sample output:
=====================================
Testing a range of L1 regularization
L1: 1e-06 - Num of Features: 4197
L1: 1e-05 - Num of Features: 2741
L1: 1e-04 - Num of Features: 46
L1: 1e-03 - Num of Features: 13
=====================================

Inverting hashes of important features (l1 = 1e-03)
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

import os, sys
import argparse

def extract_features(fp):
    text = open(fp).read().split('\n:0\n',1)[1].strip()
    if '\n' not in text:
        return []
    else:
        return text.splitlines()

def get_important_features(log_file, ml_args, warmstart_model=None, max_num_features=20):
    
    invHash_fp = log_file+'.invHash.txt'
    readModel_fp = log_file+'.readModel.txt'
 
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
    
    vw_base = 'vw ' + ml_args + ' --dsjson {} --readable_model {} --quiet'.format(log_file, readModel_fp)
    if warmstart_model:
        vw_base += ' -i {}'.format(warmstart_model)
    
    print('\n=====================================')
    print('Testing a range of L1 regularization')
    results = []
    while True:
        
        vw_cmd = vw_base + ' -c --l1 {}'.format(l1)       
        os.system(vw_cmd)
        
        num_hashed_features = len(extract_features(readModel_fp))
        results.append((l1, num_hashed_features))
        print('L1: {:.0e} - Num of Features: {}'.format(l1, num_hashed_features))
    
        if num_hashed_features < max_num_features:
            break
        else:
            l1 *= 10
    print('=====================================')
    print()
    
    if results[0][1] == 0:
        print('No features found. L1 parameter should be smaller than {:.0e}'.format(results[0][0]), file=sys.stderr)
        sys.exit(1)
    
    if results[-1][1] == 0:
        l1 = results[-2][0]
    else:
        l1 = results[-1][0]
    
    print('Inverting hashes of important features (l1 = {:.0e})'.format(l1))
    vw_cmd = vw_base + ' --invert_hash {} --l1 {}'.format(invHash_fp, l1)       
    os.system(vw_cmd)
    
    num_hashed_features = len(extract_features(readModel_fp))
    features = extract_features(invHash_fp)
    if len(features) != num_hashed_features:
        print('Error: {} features found. Expected: {}. A larger log file should be used. Aborting!'.format(len(features), num_hashed_features), file=sys.stderr)
        sys.exit(1)

    print('=====================================')
    for x in features:
        print(x)
    print('=====================================')

if __name__ == "__main__":
    parser = argparse.ArgumentParser()

    parser.add_argument('-d', '--data', type=str, help="input log file.", required=True)
    parser.add_argument('--ml_args', help="ML arguments (default: --cb_adf -l 0.01)", default='--cb_adf -l 0.01')
    parser.add_argument('-m', '--model', type=str, help="VW warmstart_model.", default=None)
    parser.add_argument('-n', '--max_num_features', type=str, help="Number of max number of important features.", default=20)

    args = parser.parse_args()

    get_important_features(args.data, args.ml_args, args.model, args.max_num_features)