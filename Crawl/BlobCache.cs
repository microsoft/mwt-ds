//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Crawl
{
    public sealed class BlobCache
    {
        private readonly CloudBlobClient blobClient;

        public BlobCache(string storageConnectionString)
        {
            var account = CloudStorageAccount.Parse(storageConnectionString);
            this.blobClient = account.CreateCloudBlobClient();
        }

        private async Task<CloudBlobContainer> GetContainer(DateTime now, string service)
        {
            var container = this.blobClient.GetContainerReference(ToContainerName(now, service));
            await container.CreateIfNotExistsAsync();

            return container;
        }

        public static string ToContainerName(DateTime now, string service) => $"{now:yyyyMM}{service}".ToLowerInvariant();

        public static string ToBlobName(string site, string id)
        {
            // escape for blob name
            id = id.Replace("//", "__")
                .Replace(":", "_");

            // https://docs.microsoft.com/en-us/rest/api/storageservices/fileservices/naming-and-referencing-containers--blobs--and-metadata
            var maxIdLength = 1024 - (site.Length + 1);
            if (id.Length > maxIdLength)
                id = id.Substring(0, maxIdLength);

            // <site>/<url>
            var sb = new StringBuilder();
            sb.Append(site);
            if (!id.StartsWith("/"))
                sb.Append('/');
            sb.Append(id);

            return sb.ToString();
        }

        public async Task<BlobContent> GetAsync(string site, string id, string service, string input, TimeSpan refreshTimeSpan, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            CacheItem cacheItem = null;
            CloudBlockBlob currentBlob = null;

            for (int i = 0; i < 2 && cacheItem == null; i++)
            {
                var container = await this.GetContainer(now.AddMonths(-i), service);
                var blobName = ToBlobName(site, id);
                var blob = container.GetBlockBlobReference(blobName);
                if (currentBlob == null)
                    currentBlob = blob;

                // TODO: CreateIfNotExists() and check for empty
                if (await blob.ExistsAsync())
                {
                    var json = await blob.DownloadTextAsync(cancellationToken);
                    cacheItem = JsonConvert.DeserializeObject<CacheItem>(json);

                    // replicate in current month
                    if (i > 0)
                        await currentBlob.UploadTextAsync(json, cancellationToken);

                    // if it isn't up for refresh, just return the existing
                    if (cacheItem.NextRefreshTimestamp > DateTime.UtcNow)
                        return new BlobContent
                        {
                            Value = cacheItem.Output,
                            Expires = cacheItem.NextRefreshTimestamp
                        };
                }
            } 

            if (cacheItem == null)
                cacheItem = new CacheItem();

            cacheItem.Input = input;
            cacheItem.NextRefreshTimestamp = DateTime.UtcNow + refreshTimeSpan;

            await currentBlob.UploadTextAsync(JsonConvert.SerializeObject(cacheItem), cancellationToken);

            return null;
        }

        public async Task<BlobContent> PersistAsync(string site, string id, string service, string input, string output, TimeSpan refreshTimeSpan, CancellationToken cancellationToken)
        {
            var container = await this.GetContainer(DateTime.UtcNow, service);
            var blobName = ToBlobName(site, id);
            var blob = container.GetBlockBlobReference(blobName);

            var cacheItem = new CacheItem
            {
                NextRefreshTimestamp = DateTime.UtcNow + refreshTimeSpan,
                // put input in there to to be consistent
                Input = input,
                Output = output
            };

            await blob.UploadTextAsync(JsonConvert.SerializeObject(cacheItem), cancellationToken);

            return new BlobContent
            {
                Value = output,
                Expires = cacheItem.NextRefreshTimestamp
            };
        }
    }
}
