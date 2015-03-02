using MultiWorldTesting;
using Newtonsoft.Json;
using System;

namespace ClientDecisionService
{
    /// <summary>
    /// Configuration object for the client decision service which contains settings for batching, retry storage, etc...
    /// </summary>
    public class DecisionServiceConfiguration<TContext>
    {
        public DecisionServiceConfiguration(string appId, string authorizationToken, IExplorer<TContext> explorer)
        {
            this.appId = appId;
            this.authorizationToken = authorizationToken;
            this.explorer = explorer;

            ContextJsonSerializer = x => JsonConvert.SerializeObject(x);

            // TODO: Choose proper default configuration for batching
            BatchConfig = new BatchingConfiguration()
            {
                MaxBufferSizeInBytes = 4 * 1024 * 1024,
                MaxDuration = TimeSpan.FromMinutes(1),
                MaxEventCount = 10000,
                MaxUploadQueueCapacity = 100,
                UploadRetryPolicy = BatchUploadRetryPolicy.Retry
            };
        }
        public string AppId { get { return appId; } }
        public string AuthorizationToken { get { return authorizationToken; } }
        public IExplorer<TContext> Explorer { get { return explorer; } }
        public bool IsPolicyUpdatable { get; set; }
        public BatchingConfiguration BatchConfig { get; set; }
        public Func<TContext, string> ContextJsonSerializer { get; set; }

        private string appId;
        private string authorizationToken;
        private IExplorer<TContext> explorer;
    }
}
