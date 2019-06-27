import functools
import itertools
import json
import re


def serialize(opts):
    if not isinstance(opts, dict):
        raise Error('opts are not dict')
    return json.dumps(opts)


def deserialize(s):
    candidate = json.loads(s)
    if not isinstance(candidate, dict):
        raise Error('candidate opts are not dict')
    return candidate


def to_commandline(opts):
    command = ''
    for key, val in opts.items():
        command = ' '.join([
            command,
            key if not key.startswith('#') else '', str(val)
        ])
    return re.sub(' +', ' ', command)


def generalize(c):
    c.pop('-f', None)
    c.pop('-i', None)
    c.pop('--cache_file', None)
    c.pop('-p', None)
    c.pop('-d', None)


def apply(first, second):
    return dict(first, **second)


def product(*dimensions):
    result = functools.reduce(
        lambda d1, d2: map(
            lambda tuple: apply(tuple[0], tuple[1]),
            itertools.product(d1, d2)
        ), dimensions)
    return list({to_commandline(c): c for c in result}.values())


def dimension(name, values):
    return list(map(lambda v: dict([(name, str(v))]), values))


if __name__ == '__main__':
    multiprocessing.freeze_support()
