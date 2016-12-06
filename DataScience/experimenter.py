from subprocess import Popen, PIPE, check_output, STDOUT, TimeoutExpired
import re
from multiprocessing import Pool, Process, Queue
import sys
import json
import queue
import threading
from datetime import datetime

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
        if marginal_list != "":
            full_command = "{0} --marginal {1}".format(full_command, ''.join(marginal_list))
        if ignore_list != "":
            for ignored_namespace in ignore_list:
                full_command = "{0} --ignore {1}".format(full_command, ignored_namespace)
        if interaction_list != "":
            for interaction in interaction_list:
                full_command = "{0} -q {1}".format(full_command, interaction)
        if regularization != "":
            full_command = "{0} --l1 {1}".format(full_command, regularization)
        self.full_command = full_command
        self.loss = None
        
def result_writer(queue):
    experiment_file = open("experiments.tsv", "a")
    # Read from queue and write results to file until done
    while True:
        msg = queue.get()
        if (msg == 'complete'):
            break
        command = msg
        line = "{0:7f}\t{1}\t{2:7f}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}".format(float(command.loss), \
            command.base, command.learning_rate, command.cb_type, str(command.marginal_list), \
            str(command.ignore_list), str(command.interaction_list), str(command.regularization), str(datetime.now()))
        experiment_file.write(line + "\n")
        experiment_file.flush()
        
    experiment_file.close()
    
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
    result_queue.put(command)
    return command
    
def run_experiment_set(command_list):
    # Run the experiments in parallel using 5 processes
    p = Pool(5)
    results = p.map(run_experiment, command_list)
    results.sort(key=lambda result: result.loss)
    return results
    
if __name__ == '__main__':
    # Identify namespaces and detect marginal features
    if len(sys.argv) < 2:
        print("Usage: python experimenter.py {file_name}. Where file_name is the merged Decision Service logs in JSON format")
        sys.exit()
    data = open(sys.argv[1], 'r', encoding="utf8")
    counter = 0
    shared_features = []
    action_features = []
    marginal_features = []
    for line in data:
        counter += 1
        event = json.loads(line)
        for feature in event.keys():
            if feature[0] != '_' and feature[0] not in shared_features:
                shared_features.append(feature[0])
        action_set = event['_multi']
        for action in action_set:
            for feature in action.keys():
                if feature[0] != '_' and feature[0] not in action_features:
                    action_features.append(feature[0])
                    if action[feature].get('constant', 0) == 1 and 'id' in action[feature]:
                        marginal_features.append(feature[0])
        if counter >= 50:
            break
            
    data.close()
    print("Shared feature namespaces: " + str(shared_features))
    print("Action feature namespaces: " + str(action_features))
    print("Marginal feature namespaces: " + str(marginal_features))

    # Start the writing process for experiment results
    result_queue = Queue()
    writer_p = Process(target=result_writer, args=((result_queue),))
    writer_p.daemon = True
    writer_p.start()        
    
    # Base parameters and setting up the cache file
    base_command = "vw --cb_adf -d %s --json -c --power_t 0" % sys.argv[1] # TODO: VW location should be a command line parameter. 
    initial_command = Command(base_command, learning_rate=0.5)
    run_experiment(initial_command, timeout=3600)
    
    # Learning rates
    command_list = []
    learning_rates = [0.5, 0.25, 0.1, 0.05, 0.025, 0.01, 0.005, 0.0025, 0.001, 0.0005, 0.00025, 0.0001, 0.00005, 0.000025, 0.00001]
    for learning_rate in learning_rates:
        command = Command(base_command, learning_rate=learning_rate)
        command_list.append(command)
        
    results = run_experiment_set(command_list)
    best_learning_rate = results[0].learning_rate
    
    # CB type
    cb_types = ['ips', 'mtr', 'dr']
    
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
        
        results = run_experiment_set(command_list)
        if results[0].loss < best_loss:
            best_loss = results[0].loss
            best_marginal_list = list(results[0].marginal_list)
        else:
            break
    
    # TODO: Which namespaces to ignore
    
    # Which namespaces to interact
    command_list = []
    best_loss = results[0].loss
    best_interaction_list = []
    all_features = shared_features + action_features
    while True:
        command_list = []
        for features in all_features:
            for action_feature in action_features:
                interaction_list = list(best_interaction_list)
                interaction = '{0}{1}'.format(features, action_feature)
                if interaction in interaction_list:
                    continue
                interaction_list.append(interaction)
                command = Command(base_command, learning_rate=best_learning_rate, cb_type=best_cb_type, marginal_list=best_marginal_list, interaction_list=interaction_list)
                command_list.append(command)
        
        results = run_experiment_set(command_list)
        if results[0].loss < best_loss:
            best_loss = results[0].loss
            best_interaction_list = list(results[0].interaction_list)
        else:
            break
        
    # Regularization
    regularizations = [1e-1, 1e-2, 1e-3, 1e-4, 1e-5, 1e-6, 1e-7, 1e-8, 1e-9, ""]
    command_list = []
    for regularization in regularizations:
        command = Command(base_command, learning_rate=best_learning_rate, cb_type=best_cb_type, marginal_list=best_marginal_list, interaction_list=best_interaction_list, regularization=regularization)
        command_list.append(command)
        
    results = run_experiment_set(command_list)
    best_regularization = results[0].regularization

    # TODO: Repeat above process of tuning parameters and interactions until convergence / no more improvements.

    # Wait for writing thread to finish
    result_queue.put('complete')
    writer_p.join()
    
    print("Best parameters:")
    print("Best learning rate: {0}".format(best_learning_rate))
    print("Best cb type: {0}".format(best_cb_type))
    print("Best marginals: {0}".format(best_marginal_list))
    print("Best interactions: {0}".format(best_interaction_list))
    print("Best regularization: {0}".format(best_regularization))