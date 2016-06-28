using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    public class AzureStorageHelper
    {
        public static void CleanCompleteBlobs()
        {
            // Delete all joined data
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(MockCommandCenter.StorageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            IEnumerable<CloudBlobContainer> completeContainers = blobClient.ListContainers("complete");

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };
            foreach (CloudBlobContainer container in completeContainers)
            {
                Parallel.ForEach(container.ListBlobs(), parallelOptions, blob => ((CloudBlockBlob)blob).Delete());
            }
        }
    }
}
