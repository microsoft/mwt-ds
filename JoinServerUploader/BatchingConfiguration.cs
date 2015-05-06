using System;

namespace JoinServerUploader
{
    /// <summary>
    /// Represents a collection of batching criteria.  
    /// </summary>
    /// <remarks>
    /// A batch is created whenever a criterion is met.
    /// </remarks>
    public class BatchingConfiguration
    {
        /// <summary>
        /// Period of time where events are grouped in one batch.
        /// </summary>
        public TimeSpan MaxDuration { get; set; }

        /// <summary>
        /// Maximum number of events in a batch.
        /// </summary>
        public int MaxEventCount { get; set; }

        /// <summary>
        /// Maximum size (in bytes) of a batch.
        /// </summary>
        public int MaxBufferSizeInBytes { get; set; }

        /// <summary>
        /// Max size of queue for processing/uploading.
        /// </summary>
        public int MaxUploadQueueCapacity { get; set; }

        /// <summary>
        /// Gets or sets the retry policy in case of upload failure.
        /// </summary>
        public BatchUploadRetryPolicy UploadRetryPolicy { get; set; }
    }

    /// <summary>
    /// Represents a retry policy for uploading events.
    /// </summary>
    public enum BatchUploadRetryPolicy
    {
        /// <summary>
        /// No retry when upload fails, data is dropped.
        /// </summary>
        None = 0,

        /// <summary>
        /// Perform an exponential-backoff retry strategy with the upload.
        /// </summary>
        ExponentialRetry
    }
}
