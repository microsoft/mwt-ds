from datetime import datetime, timedelta
from azure.storage.blob import BlockBlobService
import os

class AzureUtil:
    def __init__(self, conn_string=None, account_name=None, sas_token=None):
        if sas_token and account_name:
            self.block_blob_service = BlockBlobService(account_name=account_name, sas_token=sas_token)
        elif conn_string:
            self.block_blob_service = BlockBlobService(connection_string=conn_string)
        else
            raise Exception("No storage account credentials passed.")

    def upload_to_blob(self, storage_container_name, storage_file_name, local_file_path, throw_ex = False):
        try:
            print("\nUploading to Blob storage as blob")
            t1 = datetime.now()
            self.block_blob_service.create_blob_from_path(storage_container_name, storage_file_name, local_file_path)
            t2 = datetime.now()
            print(storage_file_name)
            print("Done uploading blob")
            print('Upload Time:',(t2-t1)-timedelta(microseconds=(t2-t1).microseconds))
        except Exception as e:
            print(e)
            if throw_ex: raise(e)
            
    def download_from_blob(self, storage_container_name, storage_file_name, local_file_path, throw_ex = False):
        try:
            print("\nDownloading from Blob container: {0} path: {1} to local path: {2}".format(storage_container_name, storage_file_name, local_file_path))
            t1 = datetime.now()
            self.block_blob_service.get_blob_to_path(storage_container_name, storage_file_name, local_file_path)
            t2 = datetime.now()
            print("Done downloading blob")
            print('Download Time:',(t2-t1)-timedelta(microseconds=(t2-t1).microseconds))
        except Exception as e:
            print(e)
            if throw_ex: raise(e)
            
    def download_all_blobs(self, storage_container_name, local_dir, throw_ex = False):
        generator = self.block_blob_service.list_blobs(storage_container_name)
        for blob in generator:
            self.download_from_blob(storage_container_name, blob.name, os.path.join(local_dir, blob.name), throw_ex)