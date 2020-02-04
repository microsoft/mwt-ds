import collections
import itertools
import json
from enum import Enum
from DashboardMpi.helpers import command


class PropType(Enum):
    NONE = 1
    BASIC = 2
    MARGINAL = 3


def detect_namespaces(j_obj, ns_set, marginal_set):
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
                if ret_val in [PropType.BASIC, PropType.MARGINAL]:
                    ns_set.update([key])
                    if ret_val is PropType.MARGINAL:
                        marginal_set.update([key])
        elif type(value) is dict:
            # Recurse on the value
            ret_val = detect_namespaces(value, ns_set, marginal_set)
            if ret_val in [PropType.BASIC, PropType.MARGINAL]:
                ns_set.update([key])
                if ret_val is PropType.MARGINAL:
                    marginal_set.update([key])
        elif value is not None:
            prop_type = PropType.BASIC

    # If basic properties were found, check if they are actually marginal properties
    if prop_type is PropType.BASIC:
        if j_obj.get('constant', 0) == 1:
            prop_type = PropType.MARGINAL

    return prop_type


def extract_namespaces(lines, log_type='cb', auto_lines=100):
    shared_tmp = collections.Counter()
    action_tmp = collections.Counter()
    marginal_tmp = collections.Counter()

    counter = 0
    for line in lines:
        if (log_type == 'cb' and not line.startswith('{"_label_cost"')) or (log_type == 'ccb' and not line.startswith('{"Timestamp"')):
            continue

        counter += 1

        event = json.loads(line)
        # Separate the shared features from the action features
        # for namespace analysis
        if 'c' in event:
            context = event['c']
            action_set = context['_multi']
            del context['_multi']
            detect_namespaces(context, shared_tmp, marginal_tmp)
            # Namespace detection expects object of type 'dict',
            # so unwrap the action list
            for action in action_set:
                detect_namespaces(action, action_tmp, marginal_tmp)
        else:
            raise ValueError('Error: c not in json:' + line)

        # We assume the schema is consistent throughout the file,
        # but since some namespaces may not appear in every datapoint,
        # check enough points.
        if counter >= auto_lines:
            break
    return (
        {x[0] for x in shared_tmp},
        {x[0] for x in action_tmp},
        {x[0] for x in marginal_tmp}
    )


def iterate_subsets(s):
    for i in range(1, len(s) + 1):
        yield from itertools.combinations(s, i)


def get_marginals_grid(name, marginals):
    marginal_args = [''] + list(map(lambda element: '--marginal ' + ''.join(element), iterate_subsets(marginals)))
    return command.dimension(name, marginal_args)


def get_interactions_grid(name, shared, actions):
    interactions = {''.join(x) for x in itertools.product(shared, actions)}
    interaction_args = [''] + list(map(lambda element: '-q ' + ' -q '.join(element), iterate_subsets(interactions)))
    return command.dimension(name, interaction_args)
