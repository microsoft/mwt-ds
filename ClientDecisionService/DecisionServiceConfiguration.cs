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
        public DecisionServiceConfiguration(string authorizationToken, IExplorer<TContext> explorer)
        {
            this.AuthorizationToken = authorizationToken;
            this.Explorer = explorer;

            this.ContextJsonSerializer = x => x == null ? null : JsonConvert.SerializeObject(x);

            // TODO: Choose proper default configuration for batching
            this.BatchConfig = new BatchingConfiguration()
            {
                MaxBufferSizeInBytes = 4 * 1024 * 1024,
                MaxDuration = TimeSpan.FromMinutes(1),
                MaxEventCount = 10000,
                MaxUploadQueueCapacity = 100,
                UploadRetryPolicy = BatchUploadRetryPolicy.Retry
            };

            LoggingServiceAddress = DecisionServiceConstants.ServiceAddress;
            LoggingServicePostAddress = DecisionServiceConstants.ServicePostAddress;
        }
        public string AuthorizationToken { get; private set; }
        public IExplorer<TContext> Explorer { get; private set; }

        #region Optional Parameters

        public bool UseLatestPolicy { get; set; }
        public string PolicyModelOutputDir { get; set; }
        public BatchingConfiguration BatchConfig { get; set; }
        public Func<TContext, string> ContextJsonSerializer { get; set; }
        public string LoggingServiceAddress { get; set; }
        public string LoggingServicePostAddress { get; set; }

        #endregion
    }
}
