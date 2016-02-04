using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class AzureBlobUpdater
    {
        public AzureBlobUpdater(string blobName, string blobAddress, string blobConnectionString, string blobOutputDir, TimeSpan pollDelay, Action<string> notifyBlobUpdate, Action<Exception> notifyPollFailure)
        {
            this.blobName = blobName;
            this.blobAddress = blobAddress;
            this.blobConnectionString = blobConnectionString;
            this.blobOutputDir = string.IsNullOrWhiteSpace(blobOutputDir) ? string.Empty : blobOutputDir;

            this.blobPollDelay = pollDelay;

            this.cancellationToken = new CancellationTokenSource();
            this.pollFinishedEvent = new ManualResetEventSlim();

            this.worker = new BackgroundWorker();
            this.worker.WorkerReportsProgress = true;
            this.worker.DoWork += PollForUpdate;
            this.worker.ProgressChanged += FoundUpdate;
            this.worker.RunWorkerAsync(this.cancellationToken);

            this.notifyBlobUpdate = notifyBlobUpdate;
            this.notifyPollFailure = notifyPollFailure;
        }

        void FoundUpdate(object sender, ProgressChangedEventArgs e)
        {
            var newblobFileName = e.UserState as string;

            this.notifyBlobUpdate(newblobFileName);
        }

        void PollForUpdate(object sender, DoWorkEventArgs e)
        {
            var cancelToken = e.Argument as CancellationTokenSource;

            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    bool cancelled = cancelToken.Token.WaitHandle.WaitOne(this.blobPollDelay);
                    if (cancelled)
                    {
                        Trace.TraceInformation("Polling for {0} cancel request received while sleeping.", this.blobName);
                        break;
                    }

                    try
                    {
                        CloudStorageAccount storageAccount = null;
                        bool accountFound = CloudStorageAccount.TryParse(this.blobConnectionString, out storageAccount);
                        if (!accountFound || storageAccount == null)
                        {
                            throw new Exception("Could not connect to Azure storage while polling for " + this.blobName);
                        }
                        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                        ICloudBlob blob = blobClient.GetBlobReferenceFromServer(new Uri(this.blobAddress),
                            new AccessCondition { IfNoneMatchETag = this.blobEtag });

                        if (blob.Properties != null)
                        {
                            this.blobEtag = blob.Properties.ETag;
                        }

                        // Write blob to file
                        if (!string.IsNullOrWhiteSpace(this.blobOutputDir))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(this.blobOutputDir));
                        }

                        string blobFileName = Path.Combine(this.blobOutputDir, this.blobName + "-" + blob.Name);

                        FileMode createMode = File.Exists(blobFileName) ? FileMode.Truncate : FileMode.CreateNew;
                        blob.DownloadToFile(blobFileName, createMode);

                        Trace.TraceInformation("Retrieved new blob for {0}", this.blobName);

                        // Notify caller of blob update
                        worker.ReportProgress(0, blobFileName);
                    }
                    catch (Exception ex)
                    {
                        var errorMessages = new List<string>(new string[]
                        {
                            "Failed to retrieve new blob information for " + this.blobName + " at " + this.blobAddress,
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
                            Trace.TraceError("Unable to write blob to disk due to restricted access. Polling for {0} will stop", this.blobName);
                            cancelToken.Cancel();
                        }
                        if (logErrors)
                        {
                            foreach (string message in errorMessages)
                            {
                                Trace.TraceError(message);
                            }
                        }

                        if (this.notifyPollFailure != null)
                        {
                            this.notifyPollFailure(ex);
                        }
                    }
                }
            }
            finally
            {
                this.pollFinishedEvent.Set();
                Trace.TraceInformation("Blob polling for {0} has been cancelled per request", this.blobName);
            }
        }

        public void StopPolling()
        {
            this.cancellationToken.Cancel();

            if (!this.pollFinishedEvent.Wait(DecisionServiceConstants.PollCancelWait))
            {
                Trace.TraceWarning("Timed out waiting for {0} blob polling task: {1} ms.", this.blobName, DecisionServiceConstants.PollCancelWait);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                if (this.worker != null)
                {
                    this.worker.Dispose();
                    this.worker = null;
                }
            }
        }

        readonly string blobName;
        readonly string blobAddress;
        readonly string blobConnectionString;
        readonly string blobOutputDir;
        readonly TimeSpan blobPollDelay;
        readonly Action<string> notifyBlobUpdate;
        readonly Action<Exception> notifyPollFailure;

        string blobEtag;
        BackgroundWorker worker;
        readonly CancellationTokenSource cancellationToken;
        readonly ManualResetEventSlim pollFinishedEvent;
    }
}
