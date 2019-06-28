import copy
import os
from helpers import command


class move_to_folder_path_generator:
    def __init__(self, folder, create=True):
        self.folder = folder
        if create:
            os.makedirs(folder, exist_ok=True)

    def get(self, input, suffix):
        return os.path.join(self.folder, os.path.basename(input)) + '.' + suffix


class model_path_generator(move_to_folder_path_generator):
    def __init__(self, folder, create=True):
        super().__init__(folder, create)

    def get_folder(self, command):
        return os.path.join(self.folder, _hash(command))

    def get(self, cache, command):
        tmp = move_to_folder_path_generator(self.get_folder(command))
        return tmp.get(cache, 'vw')


class cache_path_generator(move_to_folder_path_generator):
    def __init__(self, folder, create=True):
        super().__init__(folder, create)

    @staticmethod
    def _generate_name(input_path):
        tmp1 = os.path.split(input_path)
        tmp2 = os.path.split(tmp1[0])
        return os.path.split(tmp2[0])[1] + tmp2[1] + tmp1[1]

    def get(self, input_path):
        return super().get(cache_path_generator._generate_name(input_path), 'cache')


class pred_path_generator(move_to_folder_path_generator):
    def __init__(self, folder, create=True):
        super().__init__(folder, create)

    def get_folder(self, cache):
        return os.path.join(self.folder, os.path.basename(cache))

    def get(self, cache, policy_name):
        tmp = move_to_folder_path_generator(self.get_folder(cache))
        return tmp.get(policy_name, 'pred')


def _hash(c):
    tmp = copy.deepcopy(c)
    command.generalize(tmp)
    return command.to_commandline(tmp).replace(' ', '')


if __name__ == '__main__':
    multiprocessing.freeze_support()
