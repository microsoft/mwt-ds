using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Download blobs in background.
    /// </summary>
    public class AzureBlobBackgroundDownloader : IDisposable
    {
        /// <summary>
        /// Download finished event handler.
        /// </summary>
        public delegate void DownloadedEventHandler(object sender, byte[] data);

        /// <summary>
        /// Download failed event handler.
        /// </summary>
        public delegate void FailedEventHandler(object sender, Exception e);

        /// <summary>
        /// Download finished event handler.
        /// </summary>
        public event DownloadedEventHandler Downloaded;

        /// <summary>
        /// Download failed event handler.
        /// </summary>
        public event FailedEventHandler Failed;

        private IDisposable disposable;

        private readonly Uri blobAddress;
        private readonly CloudBlobClient cloudBlobClient;
        private string blobEtag;

        private bool downloadImmediately;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public AzureBlobBackgroundDownloader(string blobAddress, TimeSpan interval, bool downloadImmediately = false, string storageConnectionString = null)
        {
            if (blobAddress == null)
                throw new ArgumentNullException("blobAddress");

            this.blobAddress = new Uri(blobAddress);
            
            if (storageConnectionString != null)
                this.cloudBlobClient = CloudStorageAccount.Parse(storageConnectionString).CreateCloudBlobClient();

            // run background threadW
            var conn = Observable.Interval(interval)
                .SelectMany(_ => Observable.FromAsync(this.Execute))
                .Replay();

            this.disposable = conn.Connect();

            this.downloadImmediately = downloadImmediately;
        }

        private async Task Execute(CancellationToken cancellationToken)
        {
            var uri = string.Empty;
            try
            {
                ICloudBlob blob =
                    this.cloudBlobClient != null ?
                    this.cloudBlobClient.GetBlobReferenceFromServer(this.blobAddress) :
                    new CloudBlockBlob(this.blobAddress);

                // avoid not found exception
                if (!await blob.ExistsAsync(cancellationToken))
                    return;

                if (blob.Properties != null)
                {
                    // if downloadImmediately is set to false, the downloader
                    // will not download the blob on first check, and on second check
                    // onwards, the blob must have changed before a download is triggered.
                    // this is to support caller who manually downloads the blob first for
                    // other purposes and do not want to redownload.

                    // avoid not modified exception
                    if (blob.Properties.ETag == this.blobEtag)
                        return;

                    var currentBlobEtag = this.blobEtag;
                    this.blobEtag = blob.Properties.ETag;

                    // don't fire the first time...
                    if (currentBlobEtag == null && !this.downloadImmediately)
                        return;
                }

                // download
                using (var ms = new MemoryStream())
                {
                    await blob.DownloadToStreamAsync(ms, cancellationToken);
                    
                    Trace.TraceInformation("Retrieved new blob for {0}", blob.Uri);
                    
                    var evt = this.Downloaded;
                    if (evt != null)
                        evt(this, ms.ToArray());
                }
            }
            catch (Exception ex)
            {
                if (ex is StorageException)
                {
                    RequestResult result = ((StorageException)ex).RequestInformation;
                    if (result.HttpStatusCode != (int)HttpStatusCode.NotFound)
                    {
                        Trace.TraceError(
                          "Failed to retrieve '{0}': {1}. {2}",
                          uri, ex.Message, result.HttpStatusMessage);
                    }

                }
                else
                    Trace.TraceError("Failed to retrieve '{0}': {1}", uri, ex.Message);

                var evt = this.Failed;
                if (evt != null)
                    evt(this, ex);
            }
        }

        /// <summary>
        /// Disposes the object.
        /// </summary>
        public void Dispose()
        {
            if (this.disposable != null)
            {
                this.disposable.Dispose();
                this.disposable = null;
            }
        }
    }
}
