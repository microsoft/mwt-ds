import subprocess
import sys
from helpers import command


def _safe_to_float(str, default):
    try:
        return float(str)
    except (ValueError, TypeError):
        return default


def _cache(input, opts, env):
    opts['-d'] = input
    opts['--cache_file'] = env.cache_path_gen.get(input)
    return (opts, run(build_command(env.vw_path, opts), env.logger))


def _cache_func(input):
    return _cache(input[0], input[1], input[2])


def _cache_multi(opts, env):
    input_files = env.txt_provider.get()
    inputs = list(map(lambda i: (i, opts, env), input_files))
    result = env.job_pool.map(_cache_func, inputs)
    return result


def _train(cache_file, opts, env):
    opts['--cache_file'] = cache_file
    opts['-f'] = env.model_path_gen.get(cache_file, opts)
    result = (opts, run(build_command(env.vw_path, opts), env.logger))
    return result


def _train_func(input):
    return _train(input[0], input[1], input[2])


def _train_multi(opts, env):
    cache_files = env.cache_provider.get()
    for c in cache_files:
        inputs = list(map(lambda o: (c, o, env), opts))
        result = env.job_pool.map(_train_func, inputs)
        opts = list(map(lambda r: r[0], result))
        for o in opts:
            o['-i'] = o['-f']
    return result


def _predict(cache_file, command_name, command, env):
    command['-p'] = env.pred_path_gen.get(cache_file, command_name)
    _train(cache_file, command, env)
    return command_name, command


def _predict_func(input):
    return _predict(input[0], input[1], input[2], input[3])


def _predict_multi(labeled_opts, env):
    cache_files = env.cache_provider.get()
    for c in cache_files:
        inputs = list(map(lambda lo: (c, lo[0], lo[1], env), labeled_opts.items()))
        labeled_opts = dict(env.job_pool.map(_predict_func, inputs))
        for k, v in labeled_opts.items():
            labeled_opts[k]['-i'] = v['-f']


def _parse_vw_output(txt):
    result = {}
    for line in txt.split('\n'):
        if '=' in line:
            index = line.find('=')
            key = line[0:index].strip()
            value = line[index + 1:].strip()
            result[key] = value
    return result


def run(command, logger):
    process = subprocess.Popen(
        command.split(),
        universal_newlines=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE
    )
    output, error = process.communicate()
    logger.debug(command)
    return _parse_vw_output(error)


def build_command(path, opts):
    return ' '.join([path, command.to_commandline(opts)])


def cache(opts, env):
    _cache_multi(opts, env)
    command.generalize(opts)


def train(opts, env):
    if not isinstance(opts, list):
        opts = [opts]

    result = _train_multi(opts, env)
    for r in result:
        command.generalize(r[0])
    return list(map(lambda r: (r[0], _safe_to_float(r[1]['average loss'], sys.float_info.max)), result))


def predict(labeled_commands, env):
    if not isinstance(labeled_commands, dict):
        labeled_commands = {'Default', labeled_commands}

    _predict_multi(labeled_commands, env)
    for kv in labeled_commands.items():
        command.generalize(kv[1])


if __name__ == '__main__':
    multiprocessing.freeze_support()
