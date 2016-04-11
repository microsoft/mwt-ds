using System;
using FluentScheduler.Model;
using System.Threading;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal sealed class AzureBlobUpdateMetadata
    {
        private readonly string blobName;
        private readonly string blobAddress;
        private readonly string blobConnectionString;
        private readonly string blobOutputDir;
        private readonly TimeSpan blobPollDelay;
        private readonly Action<string> notifyBlobUpdate;
        private readonly Action<Exception> notifyPollFailure;
        private readonly CancellationTokenSource cancelToken;

        private string blobEtag;

        private Schedule scheduler;

        internal string BlobName
        {
            get { return blobName; }
        }

        internal string BlobAddress
        {
            get { return blobAddress; }
        }

        internal string BlobConnectionString
        {
            get { return blobConnectionString; }
        }

        internal string BlobOutputDir
        {
            get { return blobOutputDir; }
        }

        internal TimeSpan BlobPollDelay
        {
            get { return blobPollDelay; }
        }

        internal Action<string> NotifyBlobUpdate
        {
            get { return notifyBlobUpdate; }
        }

        internal Action<Exception> NotifyPollFailure
        {
            get { return notifyPollFailure; }
        }

        internal string BlobEtag
        {
            get { return blobEtag; }
            set { blobEtag = value; }
        }

        internal Schedule Scheduler
        {
            get { return scheduler; }
            set { scheduler = value; }
        }

        internal CancellationTokenSource CancelToken
        {
            get { return cancelToken; }
        } 

        internal AzureBlobUpdateMetadata(
            string blobName, string blobAddress,
            string blobConnectionString, string blobOutputDir, TimeSpan pollDelay,
            Action<string> notifyBlobUpdate, Action<Exception> notifyPollFailure,
            CancellationTokenSource cancelToken = null)
        {
            this.blobName = blobName;
            this.blobAddress = blobAddress;
            this.blobConnectionString = blobConnectionString;
            this.blobOutputDir = string.IsNullOrWhiteSpace(blobOutputDir) ? string.Empty : blobOutputDir;

            this.blobPollDelay = pollDelay;

            this.notifyBlobUpdate = notifyBlobUpdate;
            this.notifyPollFailure = notifyPollFailure;

            this.cancelToken = cancelToken ?? new CancellationTokenSource();
        }
    }
}
