from datetime import datetime, timedelta
from azure.storage.blob import BlockBlobService
import os
from loggers import Logger

class AzureUtil:
    def __init__(self, conn_string=None, account_name=None, sas_token=None):
        if sas_token and account_name:
            self.block_blob_service = BlockBlobService(account_name=account_name, sas_token=sas_token)
        elif conn_string:
            self.block_blob_service = BlockBlobService(connection_string=conn_string)
        else:
            raise Exception("No storage account credentials passed.")

    def upload_to_blob(self, storage_container_name, storage_file_name, local_file_path):
        try:
            Logger.info("\nUploading to Blob storage as blob")
            t1 = datetime.now()
            self.block_blob_service.create_blob_from_path(storage_container_name, storage_file_name, local_file_path)
            t2 = datetime.now()
            Logger.info(storage_file_name)
            Logger.info("Done uploading blob")
            Logger.info('Upload Time: {}'.format((t2-t1)-timedelta(microseconds=(t2-t1).microseconds)))
        except Exception as e:
            Logger.exception("Error uploading blob to storage")
            if self.throw_ex: raise(e)
            
    def download_from_blob(self, storage_container_name, storage_file_name, local_file_path):
        try:
            Logger.info("\nDownloading from Blob container: {0} path: {1} to local path: {2}".format(storage_container_name, storage_file_name, local_file_path))
            t1 = datetime.now()
            self.block_blob_service.get_blob_to_path(storage_container_name, storage_file_name, local_file_path)
            t2 = datetime.now()
            Logger.info("Done downloading blob")
            Logger.info('Download Time: {}'.format((t2-t1)-timedelta(microseconds=(t2-t1).microseconds)))
        except Exception as e:
            Logger.exception("Error downloading from blob")
            if self.throw_ex: raise(e)
            
    def download_all_blobs(self, storage_container_name, local_dir, throw_ex = False):
        generator = self.list_blobs(storage_container_name)
        for blob in generator:
            self.download_from_blob(storage_container_name, blob.name, os.path.join(local_dir, blob.name), throw_ex)
            
    def list_blobs(self, storage_container_name):
        return self.block_blob_service.list_blobs(storage_container_name)
