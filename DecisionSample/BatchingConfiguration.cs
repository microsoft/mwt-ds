using System;

namespace DecisionSample
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
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Maximum number of events in a batch.
        /// </summary>
        public int EventCount { get; set; }

        /// <summary>
        /// Maximum size (in bytes) of a batch.
        /// </summary>
        public int BufferSize { get; set; }
    }
}
