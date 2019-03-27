from datetime import datetime, timedelta
from azure.storage.blob import BlockBlobService

class AzureUtil:
    def __init__(self, conn_string):
        self.block_blob_service = BlockBlobService(connection_string=conn_string)

    def upload_to_blob(self, storage_container_name, storage_file_name, local_file_path):
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
            
    def download_from_blob(self, storage_container_name, storage_file_name, local_file_path):
        try:
            print("\nDownloading from Blob storage to file")
            t1 = datetime.now()
            self.block_blob_service.get_blob_to_path(storage_container_name, storage_file_name, local_file_path)
            t2 = datetime.now()
            print(storage_file_name)
            print("Done downloading blob")
            print('Download Time:',(t2-t1)-timedelta(microseconds=(t2-t1).microseconds))
        except Exception as e:
            print(e)
            
    def download_all_blobs(self, storage_container_name, local_dir):
        generator = self.block_blob_service.list_blobs(storage_container_name)
        for blob in generator:
            self.download_from_blob(storage_container_name, blob.name, local_dir + "\\" + blob.name)