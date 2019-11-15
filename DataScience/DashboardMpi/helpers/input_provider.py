import datetime
import glob
import os
import copy
import sys
sys.path.append("..")
import ds_parse
import simplejson
from DashboardMpi.helpers import command


class InputProvider:
    def __init__(self, folder, create=True):
        self.folder = folder
        if create:
            os.makedirs(folder, exist_ok=True)

    def list(self, pattern):
        return sorted(list(glob.glob(pattern)))

    def new_path(self, relative_path, suffix):
        return os.path.join(self.folder, '.'.join([relative_path, suffix]))


class CachesProvider(InputProvider):
    def __init__(self, folder, create=True):
        super().__init__(folder, create)

    def list(self):
        pattern = os.path.join(self.folder, '*.cache')
        return super().list(pattern)

    def new_path(self, input_path):
        year, month, day = _get_date_from_path(input_path)
        log_file_name = _get_file_name_from_path(input_path)
        cache_file_name = year + month + log_file_name
        return super().new_path(cache_file_name, 'cache')


class LocalLogsProvider(InputProvider):
    def __init__(self, folder, create=True):
        super().__init__(folder, create)

    def list(self):
        pattern = os.path.join(self.folder, 'data', '*', '*', '*.json')
        return super().list(pattern)

    def new_path(self, blob_name, index):
        year, month, day = _get_date_from_path(blob_name)
        relative_path = os.path.join(
            'data',
            year,
            month,
            day + '_' + str(index).zfill(3)
        )
        return super().new_path(relative_path, 'json')

    def get_metadata(self, local_log_path):
        summary_path = local_log_path + '.summary'

        for x in open(local_log_path, 'rb'):
            if x.startswith(b'{"_label_cost":') and x.strip().endswith(b'}'):
                data = ds_parse.json_cooked(x)
                with open(summary_path, 'a') as f:
                    f.write(simplejson.dumps(data)+'\n')
        os.remove(local_log_path)
        os.rename(summary_path, local_log_path)


class ModelsProvider(InputProvider):
    def __init__(self, folder, create=True):
        super().__init__(folder, create)

    def new_path(self, cache_path, command):
        tmp = InputProvider(os.path.join(self.folder, _hash(command)))
        return tmp.new_path(_get_file_name_from_path(cache_path), 'vw')


class PredictionsProvider(InputProvider):
    def __init__(self, folder, create=True):
        super().__init__(folder, create)

    def list(self, log_path):
        year, month, day = _get_date_from_path(log_path)
        log_file_name = _get_file_name_from_path(log_path)
        pred_sub_directory = year + month + log_file_name
        pattern = os.path.join(self.folder,  pred_sub_directory, '*.pred')
        return super().list(pattern)

    def new_path(self, cache_path, policy_name):
        tmp = InputProvider(
            os.path.join(self.folder, _get_file_name_from_path(cache_path))
        )
        return tmp.new_path(policy_name, 'pred')


class AzureLogsProvider:
    @staticmethod
    def iterate_blobs(bbs, container, folder, start_date, end_date):
        for i in range((end_date - start_date).days + 1):
            current_date = start_date + datetime.timedelta(i)
            log_path = os.path.join(
                folder,
                'data',
                str(current_date.year),
                str(current_date.month).zfill(2),
                str(current_date.day).zfill(2)
            )
            for blob in bbs.list_blobs(container, prefix=log_path):
                yield blob
        return
        yield

    @staticmethod
    def download_blob(bbs, container, blob_name, local_log_path, start_range=None, end_range=None, max_connections=4):
        os.makedirs(os.path.dirname(local_log_path), exist_ok=True)
        bbs.get_blob_to_path(
            container,
            blob_name,
            local_log_path,
            start_range=start_range,
            end_range=end_range,
            max_connections=max_connections
        )

    def truncate_log(local_log_path):
        last_line_length = 0
        with open(local_log_path, "r+", encoding="utf-8", errors='ignore') as file:
            file.seek(0, os.SEEK_END)
            file_size = file.tell()
            pos = file_size
            while pos > 0 and file.read(1) != "\n":
                pos -= 1
                file.seek(pos, os.SEEK_SET)

            if pos > 0:
                file.seek(pos, os.SEEK_SET)
                last_line_length = file_size - pos
                file.truncate()
        return last_line_length


def _hash(c):
    tmp = copy.deepcopy(c)
    command.generalize(tmp)
    return command.to_commandline(tmp).replace(' ', '')


def _get_date_from_path(path):
    path, file = os.path.split(path)
    day = file.split('_')[0]
    path, month = os.path.split(path)
    path, year = os.path.split(path)
    return year, month, day


def _get_file_name_from_path(path):
    return os.path.splitext(os.path.basename(path))[0]
