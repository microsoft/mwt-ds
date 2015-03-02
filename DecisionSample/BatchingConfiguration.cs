using System;

namespace ClientDecisionService
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

        public BatchUploadRetryPolicy UploadRetryPolicy { get; set; }
    }

    public enum BatchUploadRetryPolicy
    { 
        None = 0,
        Retry
    }
}
