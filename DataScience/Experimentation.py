from subprocess import Popen, PIPE, check_output, STDOUT, TimeoutExpired
import re
import multiprocessing
import sys, os
import json
from datetime import datetime, timedelta
import configparser, argparse
import gzip
import itertools
import time
from math import log, pow, ceil
from enum import Enum


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
    
def run_experiment_set(command_list, n_proc):
    # Run the experiments in parallel using n_proc processes
    p = multiprocessing.Pool(n_proc)
    results = p.map(run_experiment, command_list)
    results.sort(key=lambda result: result.loss)
    p.close()
    p.join()
    del p
    result_writer(results)
    return results

# Expects j_obj to have type 'dict' and ns_set to be a set (unique elements).
# Returns a PropType object (defined below) to indicate whether basic properties
# were found or marginal properties.
class PropType(Enum):
    NONE = 1
    BASIC = 2
    MARGINAL = 3
def detect_namespaces(j_obj, ns_set, marginal_set=None):
    prop_type = PropType.NONE
    if (j_obj is None) or type(j_obj) is not dict:
        return prop_type

    # The rule is: recurse into objects until a flat list of properties is found; the
    # nearest enclosing name is the namespace
    for kv_entry in j_obj.items():
        key = kv_entry[0]
        value = kv_entry[1]

        # Ignore entries whose key begins with an '_' except _text
        if key[0] == '_' and key != '_text':
            continue

        if type(value) is list:
            # Unwrap lists so we retain knowledge of the enclosing key name
            for item in value:
                ret_val = detect_namespaces(item, ns_set, marginal_set)
                if ret_val is PropType.BASIC:
                    ns_set.add(key[0])
                elif (marginal_set != None) and (ret_val is PropType.MARGINAL):
                    marginal_set.add(key[0])
        elif type(value) is dict:
            # Recurse on the value
            ret_val = detect_namespaces(value, ns_set, marginal_set)
            if ret_val is PropType.BASIC:
                ns_set.add(key[0])
            elif (marginal_set != None) and (ret_val is PropType.MARGINAL):
                marginal_set.add(key[0])
        elif value is not None:
            prop_type = PropType.BASIC

    # If basic properties were found, check if they are actually marginal properties
    if prop_type is PropType.BASIC:
        if (j_obj.get('constant', 0) == 1) and ('id' in j_obj):
            prop_type = PropType.MARGINAL

    return prop_type

# Ensures min <= max and min > 0
def check_min_max(val):
    [val_min, val_max] = sorted([float(i) for i in val.split(',')])
    if (val_min <= 0) or (val_min > val_max):
        raise argparse.ArgumentTypeError("{0} is an invalid range (make sure both values are positive and min <= max".format(val))
    return val


if __name__ == '__main__':

    parser = argparse.ArgumentParser()
    parser.add_argument('-f','--file_path', help="data file", required=True)
    parser.add_argument('-q','--max_q_terms', type=int, help="number of quadratic terms to explore with brute-force (default: 2)", default=2)
    parser.add_argument('-p','--n_proc', type=int, help="number of parallel processes to use (default: auto-detect)", default=multiprocessing.cpu_count()-1)
    # Construct default VW path in platform-agnostic away
    vwPath = os.path.join('.', 'vw')
    parser.add_argument('-b','--base_command', help="base command (default: vw --cb_adf --dsjson -c -d )", default=vwPath + ' --cb_adf --dsjson -c -d ')
    parser.add_argument('-l','--lr_min_max', type=check_min_max, help="learning rate range as positive values 'min,max' (default: 1e-5,0.5)", default='1e-5,0.5')
    parser.add_argument('-r','--reg_min_max', type=check_min_max, help="L1 regularization range as positive values 'min,max' (default: 1e-9,0.1)", default='1e-9,0.1')
    parser.add_argument('-s','--shared_namespaces', type=str, help="shared feature namespaces; e.g., 'abc' means namespaces a, b, and c (default: auto-detect)", default='')
    parser.add_argument('-a','--action_namespaces', type=str, help="action feature namespaces (default: auto-detect)", default='')
    parser.add_argument('-m','--marginal_namespaces', type=str, help="marginal feature namespaces (default: auto-detect)", default='')
    parser.add_argument('--auto_lines', type=int, help="number of lines to scan for auto detectdetected parameters (default: 100)", default=100)
    parser.add_argument('--only_lr', help="sweep only over the learning rate", action='store_true')

    args = parser.parse_args()
    file_path = args.file_path
    max_q_terms = args.max_q_terms
    n_proc = args.n_proc
    base_command = args.base_command + ('' if args.base_command[-1] == ' ' else ' ') + file_path
    [lr_min, lr_max] = sorted([float(i) for i in args.lr_min_max.split(',')])
    [reg_min, reg_max] = sorted([float(i) for i in args.reg_min_max.split(',')])
    only_lr = args.only_lr
    shared_features = set(list(args.shared_namespaces))
    action_features = set(list(args.action_namespaces))
    marginal_features = set(list(args.marginal_namespaces))
    auto_lines = args.auto_lines

    # Identify namespaces and detect marginal features (unless already specified)
    if not (shared_features and action_features and marginal_features):
        shared_tmp = set()
        action_tmp = set()
        marginal_tmp = set()
        with gzip.open(file_path, 'rt', encoding='utf8') if file_path.endswith('.gz') else open(file_path, 'r', encoding="utf8") as data:
            counter = 0
            for line in data:
                counter += 1
                event = json.loads(line)
                # Separate the shared features from the action features for namespace analysis
                context = event['c']
                action_set = context['_multi']
                del context['_multi']
                detect_namespaces(context, shared_tmp, marginal_tmp)
                # Namespace detection expects object of type 'dict', so unwrap the action list 
                for action in action_set:
                    detect_namespaces(action, action_tmp, marginal_tmp)

                # We assume the schema is consistent throughout the file, but since some
                # namespaces may not appear in every datapoint, check enough points.
                if counter >= auto_lines:
                    break
        # Only overwrite the namespaces that were not specified by the user
        if not shared_features:
            shared_features = shared_tmp
        if not action_features:
            action_features = action_tmp
        if not marginal_features:
            marginal_features = marginal_tmp

    print("Base command: " + base_command)
    print("Learning rate range: [{0},{1}]".format(lr_min, lr_max))
    print("L1 regularization range: [{0},{1}]".format(reg_min, reg_max))
    print("Shared feature namespaces: " + str(shared_features))
    print("Action feature namespaces: " + str(action_features))
    print("Marginal feature namespaces: " + str(marginal_features))

    input('\nPress ENTER to start...')

    t0 = datetime.now()
    
    # Read config file to get certain parameter values for experimentation
    #config = configparser.ConfigParser()
    #config.read('ds.config')
    #experiments_config = config['Experimentation']
   
    if not os.path.exists(file_path + '.cache'):
        print('Setting up the cache file')
        initial_command = Command(base_command, learning_rate=0.5)
        run_experiment(initial_command, timeout=3600)

    # Learning rate: tune this initially so we have something to use while evaluating
    # other parameters. We will retune this again at the end.
    command_list = []
    # Test learning rates separated by powers of 2
    learning_rates = [lr_min*pow(2,i) for i in range(ceil(log(lr_max/lr_min, 2)))]
    if learning_rates[-1] != lr_max:
        learning_rates.append(lr_max)
    for learning_rate in learning_rates:
        command = Command(base_command, learning_rate=learning_rate)
        command_list.append(command)
        
    results = run_experiment_set(command_list, n_proc)
    best_learning_rate = results[0].learning_rate
    
    if only_lr:
        sys.exit()
    
    # CB type
    cb_types = ['ips', 'dr']
    
    command_list = []
    for cb_type in cb_types:
        command = Command(base_command, learning_rate=best_learning_rate, cb_type=cb_type)
        command_list.append(command)
        
    results = run_experiment_set(command_list, n_proc)
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

        results = run_experiment_set(command_list, n_proc)
        if results[0].loss < best_loss:
            best_loss = results[0].loss
            best_marginal_list = list(results[0].marginal_list)
        else:
            break
    
    best_loss = results[0].loss
    
    # TODO: Which namespaces to ignore
    # Which namespaces to interact
    
    # Test all combinations up to max_q_terms
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
    results = run_experiment_set(command_list, n_proc)
    if results[0].loss < best_loss:
        best_loss = results[0].loss
        best_interaction_list = list(results[0].interaction_list)
    
    ###
    # Build greedily from the best parameters found above
    ###
    
    temp_interaction_list = list(best_interaction_list)
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

        results = run_experiment_set(command_list, n_proc)
        if results[0].loss < best_loss:
            best_loss = results[0].loss
            best_interaction_list = list(results[0].interaction_list)
        temp_interaction_list = list(results[0].interaction_list)
        
    # Regularization
    # Test regularizations separated by powers of 10
    regularizations = [reg_min*pow(10,i) for i in range(ceil(log(reg_max/reg_min, 10)))]
    if regularizations[-1] != reg_max:
        regularizations.append(reg_max)
    command_list = []
    for regularization in regularizations:
        command = Command(base_command, learning_rate=best_learning_rate, cb_type=best_cb_type, marginal_list=best_marginal_list, interaction_list=best_interaction_list, regularization=regularization)
        command_list.append(command)
        
    results = run_experiment_set(command_list, n_proc)
    best_regularization = results[0].regularization
    
    # Learning rates
    command_list = []
    learning_rates = [lr_min*pow(2,i) for i in range(ceil(log(lr_max/lr_min, 2)))]
    for learning_rate in learning_rates:
        command = Command(base_command, learning_rate=learning_rate, cb_type=best_cb_type, marginal_list=best_marginal_list, interaction_list=best_interaction_list, regularization=regularization)
        command_list.append(command)
        
    results = run_experiment_set(command_list, n_proc)
    best_learning_rate = results[0].learning_rate

    # TODO: Repeat above process of tuning parameters and interactions until convergence / no more improvements.

    elapsed_time = datetime.now() - t0
    elapsed_time -= timedelta(microseconds=elapsed_time.microseconds)
    print("\nBest parameters found after elapsed time {0}:".format(elapsed_time))
    print("Best learning rate: {0}".format(best_learning_rate))
    print("Best cb type: {0}".format(best_cb_type))
    print("Best marginals: {0}".format(best_marginal_list))
    print("Best interactions: {0}".format(best_interaction_list))
    print("Best regularization: {0}".format(best_regularization))
    print("Best loss: {0}".format(best_loss))
