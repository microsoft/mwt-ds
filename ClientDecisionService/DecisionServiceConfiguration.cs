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
            if (authorizationToken == null)
            {
                throw new ArgumentNullException("Authorization token cannot be null");
            }

            if (explorer == null)
            {
                throw new ArgumentNullException("Exploration algorithm cannot be null");
            }

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
            CommandCenterAddress = DecisionServiceConstants.CommandCenterAddress;
        }
        public string AuthorizationToken { get; private set; }
        public IExplorer<TContext> Explorer { get; private set; }

        #region Optional Parameters

        public bool OfflineMode { get; set; }
        public ILogger<TContext> Logger { get; set { if (value == null) throw new ArgumentNullException("Logger cannot be null"); } }
        public string BlobOutputDir { get; set { if (value == null) throw new ArgumentNullException("Blob output directory cannot be null"); } }
        public BatchingConfiguration BatchConfig { get; set { if (value == null) throw new ArgumentNullException("Batch configuration cannot be null"); } }
        public Func<TContext, string> ContextJsonSerializer { get; set { if (value == null) throw new ArgumentNullException("Custom JSON serializer cannot be null"); } }
        public string LoggingServiceAddress { get; set { if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException("Logging service address cannot be empty"); } }
        public string CommandCenterAddress { get; set { if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException("Command center address cannot be empty"); } }

        #endregion
    }
}
