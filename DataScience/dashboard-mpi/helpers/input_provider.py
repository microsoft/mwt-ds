import datetime
import glob
import os
from azure.storage.blob import BlockBlobService


class cache_provider:
    def __init__(self, folder):
        self.folder = folder

    def get(self):
        pattern = os.path.join(self.folder, '*.cache')
        return sorted(list(glob.glob(pattern)))


class LogsExtractor:
    @staticmethod
    def _get_log_relative_path(date):
        return 'data/%s/%s/%s_0.json' % (
            str(date.year),
            str(date.month).zfill(2),
            str(date.day).zfill(2)
        )

    @staticmethod
    def iterate_files(folder, start_date, end_date):
        for i in range((end_date - start_date).days + 1):
            current_date = start_date + datetime.timedelta(i)
            log_relative_path = LogsExtractor._get_log_relative_path(
                current_date
            )
            log_path = os.path.join(folder, log_relative_path)
            if os.path.isfile(log_path):
                yield log_path
        return
        yield

    @staticmethod
    def iterate_blobs(bbs, container, folder, start_date, end_date):
        for i in range((end_date - start_date).days + 1):
            current_date = start_date + datetime.timedelta(i)
            log_relative_path = LogsExtractor._get_log_relative_path(
                current_date
            )
            log_path = folder + '/' + log_relative_path
            for blob in bbs.list_blobs(container, prefix=log_path):
                yield blob
        return
        yield


class ps_logs_provider:
    def __init__(self, folder, start, end):
        self.folder = folder
        self.start = start
        self.end = end

    def get(self):
        return list(
            LogsExtractor.iterate_files(self.folder, self.start, self.end)
        )


class azure_logs_provider(ps_logs_provider):
    @staticmethod
    def _copy(container, connection_string, folder, start, end, local_folder,
              logger):
        bbs = BlockBlobService(connection_string=connection_string)
        for blob in LogsExtractor.iterate_blobs(bbs, container, folder, start, end):
            tmp1 = os.path.split(blob.name)
            tmp2 = os.path.split(tmp1[0])
            tmp3 = os.path.split(tmp2[0])
            relative_path = os.path.join(
                'data',
                os.path.join(tmp3[1], os.path.join(tmp2[1], tmp1[1]))
            )
            full_path = os.path.join(local_folder, relative_path)
            os.makedirs(os.path.dirname(full_path), exist_ok=True)
            logger.info(blob.name + ': Downloading to ' + full_path)
            bbs.get_blob_to_path(
                container,
                blob.name,
                full_path,
                max_connections=4
            )
            logger.info(blob.name + ': Done.')

    def __init__(self, container, connection_string, folder, start, end,
                 local_folder, logger):
        azure_logs_provider._copy(container, connection_string, folder, start,
                                  end, local_folder, logger)
        super().__init__(local_folder, start, end)

    def get(self):
        return super().get()
