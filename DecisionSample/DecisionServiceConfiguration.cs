using Newtonsoft.Json;
using System;

namespace DecisionSample
{
    /// <summary>
    /// Configuration object for the client decision service which contains settings for batching, retry storage, etc...
    /// </summary>
    public class DecisionServiceConfiguration<TContext>
    {
        public DecisionServiceConfiguration(string appId, string authorizationToken, IExploreAlgorithm<TContext> explorer)
        {
            this.appId = appId;
            this.authorizationToken = authorizationToken;
            this.explorer = explorer;

            ContextJsonSerializer = x => JsonConvert.SerializeObject(x);

            // Default configuration for batching
            BatchConfig = new BatchingConfiguration()
            {
                BufferSize = 4 * 1024 * 1024,
                Duration = TimeSpan.FromMinutes(1),
                EventCount = 10000
            };
        }
        public string AppId { get { return appId; } }
        public string AuthorizationToken { get { return authorizationToken; } }
        public IExploreAlgorithm<TContext> Explorer { get { return explorer; } }
        public int ExperimentalUnitDurationInSeconds { get; set; }
        public bool IsPolicyUpdatable { get; set; }
        public BatchingConfiguration BatchConfig { get; set; }
        public Func<TContext, string> ContextJsonSerializer { get; set; }

        private string appId;
        private string authorizationToken;
        private IExploreAlgorithm<TContext> explorer;
    }
}
