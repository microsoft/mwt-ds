using Newtonsoft.Json;
using System;

namespace DecisionSample
{
    /// <summary>
    /// Configuration object for the client decision service which contains settings for batching, retry storage, etc...
    /// </summary>
    class DecisionServiceConfiguration<TContext>
    {
        public DecisionServiceConfiguration()
        {
            ContextJsonSerializer = x => JsonConvert.SerializeObject(x);

            // Default configuration for batching
            BatchConfig = new BatchingConfiguration()
            {
                BufferSize = 4 * 1024 * 1024,
                Duration = TimeSpan.FromMinutes(1),
                EventCount = 10000
            };
        }
        public string AppId { get; set; }
        public string AuthorizationToken { get; set; }
        public IExploreAlgorithm<TContext> Explorer { get; set; }
        public int ExperimentalUnitDurationInSeconds { get; set; }
        public bool IsPolicyUpdatable { get; set; }
        public BatchingConfiguration BatchConfig { get; set; }
        public Func<TContext, string> ContextJsonSerializer { get; set; }
    }
}
