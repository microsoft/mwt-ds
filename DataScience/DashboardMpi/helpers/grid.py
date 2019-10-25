from helpers import command
from helpers.command import dimension, product


class configuration:
    def __init__(self, name, promote=1, output=1):
        self.name = name
        self.promote = promote
        self.output = output


class grid:
    def __init__(self, points, config):
        self.points = points
        self.config = config


def points_from_file(fname, limit=-1):
    result = list(map(
        lambda line: command.deserialize(line), open(fname, 'r')
    ))
    return result if limit == -1 or len(result) < limit else result[:limit]


def points_to_file(points, fname):
    open(fname, 'w').writelines(map(
        lambda p: command.serialize(p) + '\n', points
    ))


def generate(interactions_grid, marginals_grid):
    hyper_points = product(
        dimension('--power_t', [0]),    # fixing power_t to 0 since this is what should be used online
        # TODO: known bug in vw, --l1 is not working properly with --save_resume. Should uncomment this once it's fixed
        # dimension('--l1', [1e-09, 1e-07, 1e-05, 0.001, 0.1]),
        dimension('-l', [1e-7, 1e-6, 1e-5, 1e-4, 1e-3, 1e-2, 1e-1, 0.5, 1, 10]),
        dimension('--cb_type', ['ips', 'mtr']),
        marginals_grid[:2]
        )

    return [
        grid(hyper_points, configuration(name='hyper1', output=1, promote=1)),
        grid(interactions_grid, configuration(name='interactions', output=1, promote=1)),
        grid(hyper_points, configuration(name='hyper2', output=1, promote=1))
    ]


if __name__ == '__main__':
    multiprocessing.freeze_support()
