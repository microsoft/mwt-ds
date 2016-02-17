using FluentScheduler;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Web.Hosting;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class AzureBlobUpdateTask : ITask, IRegisteredObject
    {
        private readonly object syncRoot = new object();

        private bool shuttingDown;
        private AzureBlobUpdateMetadata updateMetadata;

        internal AzureBlobUpdateTask(AzureBlobUpdateMetadata updateMetadata)
        {
            this.updateMetadata = updateMetadata;

            // Register this task with the hosting environment.
            // Allows for a more graceful stop of the task, in the case of IIS shutting down.
            HostingEnvironment.RegisterObject(this);
        }

        public void Execute()
        {
            lock (syncRoot)
            {
                if (shuttingDown)
                {
                    return;
                }

                var cancelToken = this.updateMetadata.CancelToken;
                if (cancelToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    CloudStorageAccount storageAccount = null;
                    bool accountFound = CloudStorageAccount.TryParse(this.updateMetadata.BlobConnectionString, out storageAccount);
                    if (!accountFound || storageAccount == null)
                    {
                        throw new Exception("Could not connect to Azure storage while polling for " + this.updateMetadata.BlobName);
                    }
                    CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                    ICloudBlob blob = blobClient.GetBlobReferenceFromServer(new Uri(this.updateMetadata.BlobAddress),
                        new AccessCondition { IfNoneMatchETag = this.updateMetadata.BlobEtag });

                    if (blob.Properties != null)
                    {
                        this.updateMetadata.BlobEtag = blob.Properties.ETag;
                    }

                    // Write blob to file
                    if (!string.IsNullOrWhiteSpace(this.updateMetadata.BlobOutputDir))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(this.updateMetadata.BlobOutputDir));
                    }

                    string blobFileName = Path.Combine(this.updateMetadata.BlobOutputDir, this.updateMetadata.BlobName + "-" + blob.Name);

                    FileMode createMode = File.Exists(blobFileName) ? FileMode.Truncate : FileMode.CreateNew;
                    blob.DownloadToFile(blobFileName, createMode);

                    Trace.TraceInformation("Retrieved new blob for {0}", this.updateMetadata.BlobName);

                    // Notify caller of blob update
                    if (this.updateMetadata.NotifyBlobUpdate != null)
                    {
                        this.updateMetadata.NotifyBlobUpdate(blobFileName);
                    }
                }
                catch (Exception ex)
                {
                    var errorMessages = new List<string>(new string[]
                    {
                        "Failed to retrieve new blob information for " + this.updateMetadata.BlobName + " at " + this.updateMetadata.BlobAddress,
                        ex.ToString()
                    });

                    bool logErrors = true;
                    if (ex is StorageException)
                    {
                        RequestResult result = ((StorageException)ex).RequestInformation;
                        switch (result.HttpStatusCode)
                        {
                            case (int)HttpStatusCode.NotFound:
                                logErrors = false;
                                break;
                            case (int)HttpStatusCode.NotModified:
                                // Exception is raised for NotModified http response but this is expected.
                                logErrors = false;
                                break;
                            default:
                                errorMessages.Add(result.HttpStatusMessage);
                                break;
                        }
                    }
                    else if (ex is UnauthorizedAccessException)
                    {
                        Trace.TraceError("Unable to write blob to disk due to restricted access. Polling for {0} will stop", this.updateMetadata.BlobName);
                        cancelToken.Cancel();
                    }
                    if (logErrors)
                    {
                        foreach (string message in errorMessages)
                        {
                            Trace.TraceError(message);
                        }
                    }

                    if (this.updateMetadata.NotifyPollFailure != null)
                    {
                        this.updateMetadata.NotifyPollFailure(ex);
                    }
                }
            }
        }

        public void Stop(bool immediate)
        {
            // Locking here will wait for the lock in Execute to be released until this code can continue.
            lock (syncRoot)
            {
                shuttingDown = true;
            }

            HostingEnvironment.UnregisterObject(this);
        }
    }
}
