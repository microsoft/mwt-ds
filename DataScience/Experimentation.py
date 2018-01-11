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
from enum import Enum
import numpy as np


class Command:
    def __init__(self, base, cb_type=None, marginal_list=None, ignore_list=None, interaction_list=None, regularization=None, learning_rate=None, power_t=None, clone_from=None):
        self.base = base
        self.loss = np.inf

        if clone_from is not None:
            # Clone initial values
            self.cb_type = clone_from.cb_type
            self.marginal_list = set(clone_from.marginal_list)
            self.ignore_list = set(clone_from.ignore_list)
            self.interaction_list = set(clone_from.interaction_list)
            self.learning_rate = clone_from.learning_rate
            self.regularization = clone_from.regularization
            self.power_t = clone_from.power_t
        else:
            # Initialize all values to vw default
            self.cb_type = 'ips'
            self.marginal_list = set()
            self.ignore_list = set()
            self.interaction_list = set()
            self.learning_rate = 0.5
            self.regularization = 0
            self.power_t = 0.5

        # Update non-None values (for set we are doing the union not a replacement)
        if cb_type is not None:
            self.cb_type = cb_type
        if marginal_list is not None:
            self.marginal_list.update(marginal_list)
        if ignore_list is not None:
            self.ignore_list.update(ignore_list)
        if interaction_list is not None:
            self.interaction_list.update(interaction_list)
        if learning_rate is not None:
            self.learning_rate = learning_rate
        if regularization is not None:
            self.regularization = regularization
        if power_t is not None:
            self.power_t = power_t

        # Create full_command
        self.full_command = self.base
        self.full_command += " --cb_type {}".format(self.cb_type)
        if self.marginal_list:
            self.full_command += " --marginal {}".format(''.join(self.marginal_list))
        for ignored_namespace in self.ignore_list:
            self.full_command += " --ignore {}".format(ignored_namespace)
        for interaction in self.interaction_list:
            self.full_command += " -q {}".format(interaction)
        self.full_command += " -l {}".format(self.learning_rate)
        self.full_command += " --l1 {}".format(self.regularization)
        self.full_command += " --power_t {}".format(self.power_t)

    def prints(self):
        print("cb type: {0}".format(self.cb_type))
        print("marginals: {0}".format(self.marginal_list))
        print("ignore list: {0}".format(self.ignore_list))
        print("interactions: {0}".format(self.interaction_list))
        print("learning rate: {0}".format(self.learning_rate))
        print("regularization: {0}".format(self.regularization))
        print("power_t: {0}".format(self.power_t))
        print("overall command: {0}".format(self.full_command))
        print("loss: {0}".format(self.loss))
        
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
    command.loss = float(m.group(1))
    print("Ave. Loss: {:12}Policy: {}".format(str(command.loss),command.full_command))
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

# Ensures validity of min_max_steps input
def check_min_max_steps(val):
    try:
        temp = val.split(',')
        if len(temp) != 3:
            raise
        val_min, val_max = map(float, temp[:2])
        steps = int(temp[2])
        if steps < 1 or val_min <= 0 or val_min >= val_max:
            raise
    except:
        raise argparse.ArgumentTypeError('Input "{}" is an invalid range (make sure that max > min > 0 and steps > 0)'.format(val))
    return val_min, val_max, steps


if __name__ == '__main__':

    parser = argparse.ArgumentParser()
    parser.add_argument('-f','--file_path', help="data file", required=True)
    parser.add_argument('-q','--max_q_terms', type=int, help="number of quadratic terms to test with brute-force (default: 2)", default=2)
    parser.add_argument('-p','--n_proc', type=int, help="number of parallel processes to use (default: auto-detect)", default=multiprocessing.cpu_count()-1)
    parser.add_argument('-b','--base_command', help="base command (default: vw --cb_adf --dsjson -c )", default='vw --cb_adf --dsjson -c ')
    parser.add_argument('-l','--lr_min_max_steps', type=check_min_max_steps, help="learning rate range as positive values 'min,max,steps' (default: 1e-5,0.5,4)", default='1e-5,0.5,4')
    parser.add_argument('-r','--reg_min_max_steps', type=check_min_max_steps, help="L1 regularization range as positive values 'min,max,steps' (default: 1e-9,0.1,5)", default='1e-9,0.1,5')
    parser.add_argument('-t','--pt_min_max_steps', type=check_min_max_steps, help="Power_t range as positive values 'min,max,steps' (default: 1e-9,0.5,5)", default='1e-9,0.5,5')
    parser.add_argument('-s','--shared_namespaces', type=str, help="shared feature namespaces; e.g., 'abc' means namespaces a, b, and c (default: auto-detect)", default='')
    parser.add_argument('-a','--action_namespaces', type=str, help="action feature namespaces (default: auto-detect)", default='')
    parser.add_argument('-m','--marginal_namespaces', type=str, help="marginal feature namespaces (default: auto-detect)", default='')
    parser.add_argument('--auto_lines', type=int, help="number of lines to scan for auto detectdetected parameters (default: 100)", default=100)
    parser.add_argument('--only_hp', help="sweep only over the learning rate", action='store_true')
    parser.add_argument('--q_greedy_stop', help="number of rounds without improvements to stop quadratic terms greedy search (default: 3)", type=int, default=3)

    # Parse input and create variables
    args_dict = vars(parser.parse_args())   # this creates a dictionary with all input CLI
    for x in args_dict:
        locals()[x] = args_dict[x]  # this is equivalent to foo = args.foo

    # Additional processing of inputs not covered by above
    base_command += ('' if base_command[-1] == ' ' else ' ') + '-d ' + file_path
    lr_min, lr_max, lr_steps = lr_min_max_steps
    learning_rates = np.logspace(np.log10(lr_min), np.log10(lr_max), lr_steps)
    reg_min, reg_max, reg_steps = reg_min_max_steps
    regularizations = np.logspace(np.log10(reg_min), np.log10(reg_max), reg_steps)
    pt_min, pt_max, pt_steps = pt_min_max_steps
    power_t_rates = np.logspace(np.log10(pt_min), np.log10(pt_max), pt_steps)
    shared_features = set(shared_namespaces)
    action_features = set(action_namespaces)
    marginal_features = set(marginal_namespaces)

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

    print("\n*********** SETTINGS ******************")
    print("Base command: {}".format(base_command))
    print()
    print('Learning rates: ['+', '.join(map(str,learning_rates))+']')
    print('L1 regularization: ['+', '.join(map(str,regularizations))+']')
    print('Power_t rates: ['+', '.join(map(str,power_t_rates))+']')
    print()
    print("Shared feature namespaces: " + str(shared_features))
    print("Action feature namespaces: " + str(action_features))
    print("Marginal feature namespaces: " + str(marginal_features))
    print("***************************************")
    if input('Press ENTER to start (any other key to exit)...' ) != '':
        sys.exit()

    best_command = Command(base_command)
    t0 = datetime.now()
    
    if ' -c ' in base_command:    
        if not os.path.exists(file_path+'.cache'):
            print('\nCreating the cache file...')
            result = run_experiment(best_command, timeout=3600)
            if result.loss < best_command.loss:
                best_command = result
    else:
        if os.path.exists(file_path+'.cache'):
            input('Warning: Cache file found, but not used (-c not in CLI). Press to continue anyway...')

    # Regularization, Learning rates, and Power_t rates grid search
    command_list = []
    for learning_rate in learning_rates:
        for regularization in regularizations:
            for power_t in power_t_rates:
                command = Command(base_command, clone_from=best_command, regularization=regularization, learning_rate=learning_rate, power_t=power_t)
                command_list.append(command)

    print('\nTesting {} different hyperparameters...'.format(len(command_list)))
    results = run_experiment_set(command_list, n_proc)
    if results[0].loss < best_command.loss:
        best_command = results[0]
    
    if only_hp:
        elapsed_time = datetime.now() - t0
        elapsed_time -= timedelta(microseconds=elapsed_time.microseconds)
        print("\nBest parameters found after elapsed time {0}:".format(elapsed_time))
        best_command.prints()
        sys.exit()
    
    # CB type
    print('\nTesting cb types...')
    cb_types = ['dr']       # ips is default (avoid to recheck it)
    command_list = []
    for cb_type in cb_types:
        command = Command(base_command, clone_from=best_command, cb_type=cb_type)
        command_list.append(command)
        
    results = run_experiment_set(command_list, n_proc)
    if results[0].loss < best_command.loss:
        best_command = results[0]

    # Add Marginals
    print('\nTesting marginals...')
    while True:
        command_list = []
        for feature in marginal_features:
            if feature in best_command.marginal_list:
                continue
            command = Command(base_command, clone_from=best_command, marginal_list={feature})
            command_list.append(command)
        
        if len(command_list) == 0:
            break

        results = run_experiment_set(command_list, n_proc)
        if results[0].loss < best_command.loss:
            best_command = results[0]
        else:
            break
        
    # TODO: Which namespaces to ignore
    # Which namespaces to interact
    
    # Test all combinations up to max_q_terms
    command_list = []
    n_sf = len(shared_features)
    n_af = len(action_features)
    for x in itertools.product([0, 1], repeat=n_sf*n_af):
        if sum(x) > max_q_terms:
            continue
    
        interaction_list = set()
        for i,features in enumerate(shared_features):
            for j,action_feature in enumerate(action_features):
                if x[i*n_af+j]:
                    interaction = '{0}{1}'.format(features, action_feature)
                    interaction_list.add(interaction)

        command = Command(base_command, clone_from=best_command, interaction_list=interaction_list)
        command_list.append(command)
    
    print('\nTesting {} different interactions...'.format(len(command_list)))
    results = run_experiment_set(command_list, n_proc)
    if results[0].loss < best_command.loss:
        best_command = results[0]
    
    # Build greedily on top of the best parameters found above (stop when no improvements for q_greedy_stop consecutive rounds)
    print('\nBuilding interactions greedily...')
    temp_interaction_list = set(best_command.interaction_list)
    rounds_without_improvements = 0
    while rounds_without_improvements < q_greedy_stop:
        command_list = []
        for features in shared_features:
            for action_feature in action_features:
                interaction = '{0}{1}'.format(features, action_feature)
                if interaction in temp_interaction_list:
                    continue
                command = Command(base_command, clone_from=best_command, interaction_list=temp_interaction_list.union({interaction}))   # union() keeps temp_interaction_list unchanged
                command_list.append(command)
        
        if len(command_list) == 0:
            break

        results = run_experiment_set(command_list, n_proc)
        if results[0].loss < best_command.loss:
            best_command = results[0]
            rounds_without_improvements = 0
        else:
            rounds_without_improvements += 1
        temp_interaction_list = set(results[0].interaction_list)

    # Regularization, Learning rates, and Power_t rates grid search
    command_list = []
    for learning_rate in learning_rates:
        for regularization in regularizations:
            for power_t in power_t_rates:
                command = Command(base_command, clone_from=best_command, regularization=regularization, learning_rate=learning_rate, power_t=power_t)
                command_list.append(command)

    print('\nTesting {} different hyperparameters...'.format(len(command_list)))
    results = run_experiment_set(command_list, n_proc)
    if results[0].loss < best_command.loss:
        best_command = results[0]

    # TODO: Repeat above process of tuning parameters and interactions until convergence / no more improvements.

    elapsed_time = datetime.now() - t0
    elapsed_time -= timedelta(microseconds=elapsed_time.microseconds)
    print("\n\n*************************")
    print("Best parameters found after elapsed time {0}:".format(elapsed_time))
    best_command.prints()
    print("*************************")
