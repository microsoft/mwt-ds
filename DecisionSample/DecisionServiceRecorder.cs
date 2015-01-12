using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiWorldTesting;
using Newtonsoft.Json;

namespace DecisionSample
{
    /// <summary>
    /// Represents a collection of batching criteria.  
    /// </summary>
    /// <remarks>
    /// A batch is created whenever a criterion is met.
    /// </remarks>
    struct BatchingConfiguration
    {
        /// <summary>
        /// Period of time where events are grouped in one batch.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Maximum number of events in a batch.
        /// </summary>
        public uint EventCount { get; set; }

        /// <summary>
        /// Maximum size (in bytes) of a batch.
        /// </summary>
        public long BufferSize { get; set; }
    }

    enum StorageEndpoint
    { 
        LocalDisk = 0,
        AzureBlob
    }

    struct RetryStorageConfiguration
    {
        public StorageEndpoint EndpointType;
        public string FilePath;
        public string AzureConnectionString;
    }

    // Could also be templatized by Outcome type
    class DecisionServiceRecorder<Context> : IRecorder<Context>
    {
        public DecisionServiceRecorder(RetryStorageConfiguration storageConfig) { }
        public DecisionServiceRecorder(RetryStorageConfiguration storageConfig, BatchingConfiguration batchConfig) { }

        public void Record(Context context, uint action, float probability, string uniqueKey) 
        {
            string contextJson = JsonConvert.SerializeObject(context);
            // . . .
        }

        public void ReportOutcome(object outcome, string uniqueKey)
        {
            string outcomeJson = JsonConvert.SerializeObject(outcome);
            // . . .
        }
    }
}
