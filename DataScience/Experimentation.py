from subprocess import Popen, PIPE, check_output, STDOUT, TimeoutExpired
import re
from multiprocessing import Pool, Process, Queue
import sys
import json
import queue
import threading
from datetime import datetime
import configparser
import gzip
import itertools
import time
import numpy as np

class Command:
    def __init__(self, base, learning_rate="", cb_type="", marginal_list="", ignore_list="", interaction_list="", regularization=""):
        self.base = base
        self.learning_rate = learning_rate
        self.cb_type = cb_type
        self.marginal_list = marginal_list
        self.ignore_list = ignore_list
        self.interaction_list = interaction_list
        self.regularization = regularization
        full_command = base
        if learning_rate != "":
            full_command = "{0} -l {1}".format(full_command, learning_rate)
        if cb_type != "":
            full_command = "{0} --cb_type {1}".format(full_command, cb_type)
        if len(marginal_list) != 0:
            full_command = "{0} --marginal {1}".format(full_command, ''.join(marginal_list))
        if len(ignore_list) != 0:
            for ignored_namespace in ignore_list:
                full_command = "{0} --ignore {1}".format(full_command, ignored_namespace)
        if len(interaction_list) != 0:
            for interaction in interaction_list:
                full_command = "{0} -q {1}".format(full_command, interaction)
        if regularization != "":
            full_command = "{0} --l1 {1}".format(full_command, regularization)
        self.full_command = full_command
        print(full_command)
        self.loss = None
        
def result_writer(command_list):
    experiment_file = open("experiments.csv", "a")
    for command in command_list:
        line = "{0:7f}\t{1}\t{2:7f}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}".format(float(command.loss), \
            command.base, command.learning_rate, command.cb_type, str(command.marginal_list), \
            str(command.ignore_list), str(command.interaction_list), str(command.regularization), str(datetime.now()), command.full_command)
        experiment_file.write(line + "\n")
    experiment_file.flush()
    
def run_experiment(command, timeout=1000):
    while True:
        try:
            results = check_output(command.full_command.split(' '), stderr=STDOUT, timeout=timeout).decode("utf-8")
            break
        except TimeoutExpired:
            print("Timeout expired for command {0}".format(command.full_command))
            
    m = re.search('average loss = (.+)\n', str(results))
    loss = m.group(1)
    command.loss = float(loss)
    return command
    
def run_experiment_set(command_list):
    # Run the experiments in parallel using 20 processes
    p = Pool(20)
    results = p.map(run_experiment, command_list)
    results.sort(key=lambda result: result.loss)
    del p
    result_writer(results)
    return results

if __name__ == '__main__':
    # Identify namespaces and detect marginal features
    if len(sys.argv) < 2:
        print("Usage: python Experimentation.py {file_name} {max_q_terms}")
        print("       file_name is the merged Decision Service logs in JSON format")
        print("       max_q_terms is the max number of quadratic terms in the brute force search (default: 3)")
        sys.exit()
        
    t0 = time.time()

    file_name = sys.argv[1]
    max_q_terms = 3
    if len(sys.argv) > 2:
        max_q_terms = int(sys.argv[2])
    print("Setting max_q_terms = ",max_q_terms)

    # a if condition else b 
    #with gzip.open(file_name, 'rt', encoding='utf8') if file_name.endswith('.gz') else open(file_name, 'r', encoding="utf8") as data:
    #    counter = 0
    #    shared_features = []
    #    action_features = []
    #    marginal_features = []
    #    for line in data:
    #        counter += 1
    #        event = json.loads(line)
    #        for feature in event.keys():
    #            if feature[0] != '_' and feature[0] not in shared_features:
    #                shared_features.append(feature[0])
    #        action_set = event['_multi']
    #        for action in action_set:
    #            for feature in action.keys():
    #                # print(type(action[feature]))
    #                if action[feature] is dict:
    #                    if feature[0] != '_' and feature[0] not in action_features:
    #                        action_features.append(feature[0])
    #                        print (str(action[feature]).encode(sys.stdout.encoding, errors='replace'))
    #                        print (feature)
    #                        if action[feature].get('constant', 0) == 1 and 'id' in action[feature]:
    #                            marginal_features.append(feature[0])
    #        # We are assuming the schema is consistent throughout the file, so we don't need to read all of it
    #        if counter >= 50:
                #break


    shared_features = ['G', 'M', 'O']               # {Geo, MRefer, OUserAgent}
    action_features = ['X', 'T', 'E', 'R', 'S']     # {XSentiment, Tags, Emotion, RVisionTags, SVisionAdult}
    marginal_features = ['i']

    # disable auto discovery    
    # shared_features = []
    # action_features = []
    # marginal_features = []

    # Read config file to get certain parameter values for experimentation
    config = configparser.ConfigParser()
    config.read('ds.config')
    experiments_config = config['Experimentation']
    
    print("Shared feature namespaces: " + str(shared_features))
    print("Action feature namespaces: " + str(action_features))
    print("Marginal feature namespaces: " + str(marginal_features))

    # Base parameters and setting up the cache file
    base_command = "vw --cb_adf -d %s --dsjson -c --power_t 0 --ignore A" % file_name # TODO: VW location should be a command line parameter. 
    # base_command = "vw --cb_explore_adf --epsilon 0.2 -d %s --json -c --power_t 0" % sys.argv[1] # TODO: VW location should be a command line parameter. 
    #  -q LG -q TG
    # base_command += " --quadratic UG --quadratic RG --quadratic AG --ignore B --ignore C --ignore D --ignore E --ignore F --marginal JK"
    initial_command = Command(base_command, learning_rate=0.5)
    run_experiment(initial_command, timeout=3600)
    
    # Learning rates
    command_list = []
    learning_rates = experiments_config["LearningRates"].split(',')
    learning_rates = list(map(float, learning_rates))
    for learning_rate in learning_rates:
        command = Command(base_command, learning_rate=learning_rate)
        command_list.append(command)
        
    results = run_experiment_set(command_list)
    best_learning_rate = results[0].learning_rate
    
    # CB type
    cb_types = ['ips', 'dr']
    
    command_list = []
    for cb_type in cb_types:
        command = Command(base_command, learning_rate=best_learning_rate, cb_type=cb_type)
        command_list.append(command)
        
    results = run_experiment_set(command_list)
    best_cb_type = results[0].cb_type

    # Add Marginals
    best_loss = results[0].loss
    best_marginal_list = []
    while True:
        command_list = []
        for feature in marginal_features:         
            marginal_list = list(best_marginal_list)
            marginal_list.append(feature)
            command = Command(base_command, learning_rate=best_learning_rate, cb_type=best_cb_type, marginal_list=marginal_list)
            command_list.append(command)
        
        if len(command_list) == 0:
            break

        results = run_experiment_set(command_list)
        if results[0].loss < best_loss:
            best_loss = results[0].loss
            best_marginal_list = list(results[0].marginal_list)
        else:
            break
    
    best_loss = results[0].loss
    
    # TODO: Which namespaces to ignore
    
    # Which namespaces to interact
    
    # Test all combinations up to max_q_terms (default: 3)
    best_interaction_list = []
    command_list = []
    
    n_sf = len(shared_features)
    n_af = len(action_features)
    for x in itertools.product([0, 1], repeat=n_sf*n_af):
        if sum(x) > max_q_terms:
            continue
    
        interaction_list = []
        for i,features in enumerate(shared_features):
            for j,action_feature in enumerate(action_features):
                if x[i*n_af+j]:
                    interaction = '{0}{1}'.format(features, action_feature)
                    interaction_list.append(interaction)

        command = Command(base_command, learning_rate=best_learning_rate, cb_type=best_cb_type, marginal_list=best_marginal_list, interaction_list=interaction_list)
        command_list.append(command)
    
    print('len(command_list)',len(command_list))
    results = run_experiment_set(command_list)
    if results[0].loss < best_loss:
        best_loss = results[0].loss
        best_interaction_list = list(results[0].interaction_list)
    
    
    # Building greedily from best found above
    
    temp_interaction_list = list(best_interaction_list)
    all_features = shared_features + action_features
    while True:
        command_list = []
        for features in shared_features:
            for action_feature in action_features:
                interaction_list = list(temp_interaction_list)
                interaction = '{0}{1}'.format(features, action_feature)
                if interaction in interaction_list:
                    continue
                interaction_list.append(interaction)
                command = Command(base_command, learning_rate=best_learning_rate, cb_type=best_cb_type, marginal_list=best_marginal_list, interaction_list=interaction_list)
                command_list.append(command)
        
        if len(command_list) == 0:
            break

        results = run_experiment_set(command_list)
        if results[0].loss < best_loss:
            best_loss = results[0].loss
            best_interaction_list = list(results[0].interaction_list)
        temp_interaction_list = list(results[0].interaction_list)
        
    # Regularization
    regularizations = experiments_config["RegularizationValues"].split(',')
    regularizations = list(map(float, regularizations))
    command_list = []
    for regularization in regularizations:
        command = Command(base_command, learning_rate=best_learning_rate, cb_type=best_cb_type, marginal_list=best_marginal_list, interaction_list=best_interaction_list, regularization=regularization)
        command_list.append(command)
        
    results = run_experiment_set(command_list)
    best_regularization = results[0].regularization
    
    # Learning rates
    command_list = []
    learning_rates = experiments_config["LearningRates"].split(',')
    learning_rates = list(map(float, learning_rates))
    for learning_rate in learning_rates:
        command = Command(base_command, learning_rate=learning_rate, cb_type=best_cb_type, marginal_list=best_marginal_list, interaction_list=best_interaction_list, regularization=regularization)
        command_list.append(command)
        
    results = run_experiment_set(command_list)
    best_learning_rate = results[0].learning_rate

    # TODO: Repeat above process of tuning parameters and interactions until convergence / no more improvements.
    
    print("Best parameters:")
    print("Best learning rate: {0}".format(best_learning_rate))
    print("Best cb type: {0}".format(best_cb_type))
    print("Best marginals: {0}".format(best_marginal_list))
    print("Best interactions: {0}".format(best_interaction_list))
    print("Best regularization: {0}".format(best_regularization))
    print("Best loss: {0}".format(best_loss))
    print("Elapsed time: {0}".format(time.time()-t0))
