using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace Microsoft.Research.MultiWorldTesting.JoinUploader
{
    public delegate void EventUploaderSuccessEventHandler(object source, int eventCount, int sumSize, int inputQueueSize);

    public delegate void EventUploaderErrorEventHandler(object source, Exception e);

    /// <summary>
    /// Represents a collection of batching criteria.  
    /// </summary>
    /// <remarks>
    /// A batch is created whenever a criterion is met.
    /// </remarks>
    public class BatchingConfiguration
    {
        /// <summary>
        /// Constructor with default configuration values set.
        /// </summary>
        public BatchingConfiguration()
        {
            this.MaxBufferSizeInBytes = 4 * 1024 * 1024;
            this.MaxDuration = TimeSpan.FromSeconds(5);
            this.MaxEventCount = 1024;
            // the number of events buffered is MaxEventCount * MaxUploadQueueCapacity * MaxDegreeOfSerializationParallelism
            this.MaxUploadQueueCapacity = 512;
            this.PartitionCount = 16;
            this.UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry;
            this.MaxDegreeOfSerializationParallelism = Environment.ProcessorCount;
            this.DroppingPolicy = new DroppingPolicy();
            this.ReUseTcpConnection = true;
        }

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


        public int? PartitionCount { get; set; }

        /// <summary>
        /// Gets or sets the retry policy in case of upload failure.
        /// </summary>
        public BatchUploadRetryPolicy UploadRetryPolicy { get; set; }

        /// <summary>
        /// Gets or sets the reference resolver to be used with JSON.NET.
        /// </summary>
        public IReferenceResolver ReferenceResolver { get; set; }

        /// <summary>
        /// Gets or sets the maxium degree of parallelism employed when serializing events.
        /// </summary>
        public int MaxDegreeOfSerializationParallelism { get; set; }

        /// <summary>
        /// Gets or sets the data dropping policy which controls which events are sent to the upload queue.
        /// </summary>
        public DroppingPolicy DroppingPolicy { get; set; }

        /// <summary>
        /// If set to true, the TCP connection for the specified event hub will be re-used.
        /// Otherwise MessageFactory will be used to create separate connections.
        /// </summary>
        /// <remarks>Defaults to true.</remarks>
        public bool ReUseTcpConnection { get; set; }

        /// <summary>
        /// Invoked if an error happened during the upload pipeline.
        /// </summary>
        public event EventUploaderErrorEventHandler ErrorHandler;

        /// <summary>
        /// Invoked after the batch was successfully uploaded.
        /// </summary>
        public event EventUploaderSuccessEventHandler SuccessHandler;

        internal void FireErrorHandler(Exception e)
        {
            var handler = this.ErrorHandler;
            if (handler != null)
                handler(this, e);
        }

        internal void FireSuccessHandler(int eventCount, int sumSize, int inputQueueSize)
        {
            var handler = this.SuccessHandler;
            if (handler != null)
                handler(this, eventCount, sumSize, inputQueueSize);
        }
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

    /// <summary>
    /// Represents settings which control how data can be dropped at high load.
    /// </summary>
    public class DroppingPolicy
    {
        /// <summary>
        /// Constructor using default settings to not drop any data.
        /// </summary>
        public DroppingPolicy()
        {
            // By default don't drop anything
            this.MaxQueueLevelBeforeDrop = 1f;
            this.ProbabilityOfDrop = 0f;
        }

        /// <summary>
        /// Gets or sets the threshold level (measured in % of total queue size) at which point
        /// data are randomly dropped by the probability specified in <see cref="ProbabilityOfDrop"/>.
        /// </summary>
        public float MaxQueueLevelBeforeDrop { get; set; }

        /// <summary>
        /// Gets or sets the probability of dropping an event. This is used to reduce the system load.
        /// </summary>
        public float ProbabilityOfDrop { get; set; }
    }
}
