import sys
import subprocess
from subprocess import check_output
from DashboardMpi.helpers import command


def _safe_to_float(str, default):
    try:
        return float(str)
    except (ValueError, TypeError):
        return default


def _cache(log_path, opts, env):
    opts['-d'] = log_path
    opts['--cache_file'] = env.caches_provider.new_path(log_path)
    return (opts, run(build_command(opts), env.logger))


def _cache_func(input):
    return _cache(input[0], input[1], input[2])


def _cache_multi(opts, env, file_path):
    input_files = [file_path]
    inputs = list(map(lambda i: (i, opts, env), input_files))
    return env.job_pool.map(_cache_func, inputs)


def _train(cache_path, opts, env):
    opts['--cache_file'] = cache_path
    opts['-f'] = env.models_provider.new_path(cache_path, opts)
    result = (opts, run(build_command(opts), env.logger))
    return result


def _train_func(input):
    return _train(input[0], input[1], input[2])


def _update_opts(r):
    r[0]['-i'] = r[0]['-f']
    return r[0]


def _process_result(r):
    command.generalize(r[0])
    return (r[0], r[1]["average loss"])


def _train_multi(opts, env):
    cache_files = env.caches_provider.list()
    for index, cache in enumerate(cache_files):
        inputs = list(map(lambda o: (cache, o, env), opts))
        result = env.job_pool.map(_train_func, inputs)

        if index == len(cache_files) - 1:
            return list(map(_process_result, result))
        else:
            opts = list(map(_update_opts, result))


def _predict(cache_path, command_name, command, env):
    command['-p'] = env.predictions_provider.new_path(cache_path, command_name)
    _train(cache_path, command, env)
    return command_name, command


def _predict_func(input):
    return _predict(input[0], input[1], input[2], input[3])


def _predict_multi(labeled_opts, env):
    cache_files = env.caches_provider.list()
    for c in cache_files:
        inputs = list(map(
            lambda lo: (c, lo[0], lo[1], env), labeled_opts.items()))
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
            if key == "average loss":
                result[key] = _safe_to_float(value, sys.float_info.max)
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
    logger.debug(error)
    return _parse_vw_output(error)


def build_command(opts):
    return command.to_commandline(opts)


def cache(opts, env, file_path):
    _cache_multi(opts, env, file_path)
    command.generalize(opts)


def train(opts, env):
    if not isinstance(opts, list):
        opts = [opts]
    return _train_multi(opts, env)


def predict(labeled_commands, env):
    if not isinstance(labeled_commands, dict):
        labeled_commands = {'Default', labeled_commands}

    _predict_multi(labeled_commands, env)
    for kv in labeled_commands.items():
        command.generalize(kv[1])


def check_vw_installed(logger):
    try:
        check_output(['vw', '-h'], stderr=subprocess.DEVNULL)
    except Exception:
        logger.error("Error: Vowpal Wabbit executable not found. Please install and add it to your path")
        sys.exit()


if __name__ == '__main__':
    multiprocessing.freeze_support()
