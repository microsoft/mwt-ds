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
        }
        public string AuthorizationToken { get; private set; }
        public IExplorer<TContext> Explorer { get; private set; }

        #region Optional Parameters

        public bool OfflineMode { get; set; }

        public ILogger<TContext> Logger 
        { 
            get { return logger; } 
            set 
            { 
                if (value == null) throw new ArgumentNullException("Logger cannot be null");
                logger = value;
            } 
        
        }
        public string BlobOutputDir 
        { 
            get { return blobOutputDir; } 
            set 
            { 
                if (value == null) throw new ArgumentNullException("Blob output directory cannot be null");
                blobOutputDir = value;
            } 
        }
        
        public BatchingConfiguration BatchConfig 
        {
            get { return batchConfig; }
            set 
            { 
                if (value == null) throw new ArgumentNullException("Batch configuration cannot be null");
                batchConfig = value;
            } 
        }
        
        public Func<TContext, string> ContextJsonSerializer 
        {
            get { return contextJsonSerializer; }
            set 
            { 
                if (value == null) throw new ArgumentNullException("Custom JSON serializer cannot be null");
                contextJsonSerializer = value;
            } 
        }
        
        public string LoggingServiceAddress 
        {
            get { return loggingServiceAddress; }
            set 
            { 
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException("Logging service address cannot be empty");
                loggingServiceAddress = value;
            } 
        }
        
        public string CommandCenterAddress 
        {
            get { return commandCenterAddress; }
            set 
            { 
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException("Command center address cannot be empty");
                commandCenterAddress = value;
            } 
        }

        private ILogger<TContext> logger;
        private string blobOutputDir;
        private BatchingConfiguration batchConfig;
        private Func<TContext, string> contextJsonSerializer;
        private string loggingServiceAddress;
        private string commandCenterAddress;

        #endregion
    }
}
