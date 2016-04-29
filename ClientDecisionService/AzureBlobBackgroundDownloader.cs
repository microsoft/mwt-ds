using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class AzureBlobBackgroundDownloader : IDisposable
    {
        public delegate void DownloadedEventHandler(object sender, byte[] data);

        public delegate void FailedEventHandler(object sender, Exception e);

        public event DownloadedEventHandler Downloaded;

        public event FailedEventHandler Failed;


        private IDisposable disposable;

        private readonly string blobAddress;

        private readonly CloudStorageAccount storageAccount;
        
        private string blobEtag;

        private bool downloadImmediately;

        public AzureBlobBackgroundDownloader(string blobConnectionString, string blobAddress, TimeSpan interval, bool downloadImmediately = false)
        {
            if (blobConnectionString == null)
                throw new ArgumentNullException("blobConnectionString");

            if (blobAddress == null)
                throw new ArgumentNullException("blobAddress");

            var accountFound = CloudStorageAccount.TryParse(blobConnectionString, out this.storageAccount);
            if (!accountFound || storageAccount == null)
                throw new ArgumentException("Invalid connection string '" + blobConnectionString + "'", "blobConnectionString");

            this.blobAddress = blobAddress;

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
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                ICloudBlob blob = await blobClient.GetBlobReferenceFromServerAsync(new Uri(this.blobAddress), cancellationToken);
                uri = blob.Uri.ToString();

                // avoid not found exception
                if (!await blob.ExistsAsync(cancellationToken))
                    return;

                if (blob.Properties != null)
                {
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
                    Trace.TraceError(
                      "Failed to retrieve '{0}': {1}. {2}",
                      uri, ex.Message, result.HttpStatusMessage);
                }
                else
                    Trace.TraceError("Failed to retrieve '{0}': {1}", uri, ex.Message);

                var evt = this.Failed;
                if (evt != null)
                    evt(this, ex);
            }
        }

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
