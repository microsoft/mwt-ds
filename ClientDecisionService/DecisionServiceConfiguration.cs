namespace ClientDecisionService
{
    using System;
    using Microsoft.Research.DecisionService.Uploader;

    public class BaseDecisionServiceConfiguration<TContext>
    {
        public BaseDecisionServiceConfiguration(string authorizationToken)
        {
            if (authorizationToken == null)
            {
                throw new ArgumentNullException("authorizationToken", "Authorization token cannot be null");
            }

            this.AuthorizationToken = authorizationToken;
        }

        /// <summary>
        /// The authorization token that is used for request authentication.
        /// </summary>
        public string AuthorizationToken { get; private set; }

        #region Optional Parameters

        /// <summary>
        /// Indicates whether to operate in offline mode where polling and join service logging are turned off.
        /// </summary>
        /// <remarks>
        /// In offline mode, a custom <see cref="IRecorder{TContext}"/> object must be defined.
        /// </remarks>
        public bool OfflineMode { get; set; }

        /// <summary>
        /// Specifies the output directory on disk for blob download (e.g. of settings or model files).
        /// </summary>
        public string BlobOutputDir
        {
            get { return blobOutputDir; }
            set
            {
                if (value == null) throw new ArgumentNullException("Blob output directory cannot be null");
                blobOutputDir = value;
            }
        }

        /// <summary>
        /// Specifies the batching configuration when uploading data to join service.
        /// </summary>
        /// <remarks>
        /// In offline mode, batching configuration will not be used since the join service recorder is turned off.
        /// </remarks>
        public BatchingConfiguration JoinServiceBatchConfiguration
        {
            get { return batchConfig; }
            set
            {
                if (value == null) throw new ArgumentNullException("Batch configuration cannot be null");
                batchConfig = value;
            }
        }

        /// <summary>
        /// Type of Join Server implementation to use.
        /// </summary>
        public JoinServerType JoinServerType { get; set; }

        /// <summary>
        /// Specifies the address for a custom HTTP logging service.
        /// </summary>
        /// <remarks>
        /// When specified, this will override the default join service logging provided by the Multiworld Testing Service.
        /// </remarks>
        public string LoggingServiceAddress
        {
            get { return loggingServiceAddress; }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException("Logging service address cannot be empty");
                loggingServiceAddress = value;
            }
        }

        /// <summary>
        /// The ASA connection string to use if ASA-based Join Server is selected.
        /// </summary>
        public string EventHubConnectionString { get; set; }

        /// <summary>
        /// The EventHub input name to use if ASA-based Join Server is selected.
        /// </summary>
        public string EventHubInputName { get; set; }

        /// <summary>
        /// Specifies the polling period to check for updated application settings.
        /// </summary>
        /// <remarks>
        /// Polling is turned off if this value is set to <see cref="TimeSpan.MinValue"/>.
        /// </remarks>
        public TimeSpan PollingForSettingsPeriod
        {
            get { return pollingForSettingsPeriod; }
            set
            {
                if (value <= TimeSpan.FromSeconds(0) && value != TimeSpan.MinValue) throw new ArgumentNullException("Invalid polling period value.");
                pollingForSettingsPeriod = value;
            }
        }

        /// <summary>
        /// Specifies the polling period to check for updated ML model.
        /// </summary>
        /// <remarks>
        /// Polling is turned off if this value is set to <see cref="TimeSpan.MinValue"/>.
        /// </remarks>
        public TimeSpan PollingForModelPeriod
        {
            get { return pollingForModelPeriod; }
            set
            {
                if (value <= TimeSpan.FromSeconds(0) && value != TimeSpan.MinValue) throw new ArgumentNullException("Invalid polling period value.");
                pollingForModelPeriod = value;
            }
        }

        /// <summary>
        /// Callback to set model Id in the Context for reproducibility.
        /// This callback is triggered when a model is used for scoring.
        /// In order to reproduce the behavior of an online system, exact
        /// models and context need to be recorded at the time the scoring
        /// decision is made from the model.
        /// </summary>
        public Action<TContext, string> SetModelIdCallback { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        public Action<Exception> ModelPollFailureCallback { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        public Action<Exception> SettingsPollFailureCallback { get; set; }

        #endregion

        private string blobOutputDir;
        private BatchingConfiguration batchConfig;
        private string loggingServiceAddress;
        private TimeSpan pollingForSettingsPeriod;
        private TimeSpan pollingForModelPeriod;
    }
}


namespace ClientDecisionService.SingleAction
{
    using Microsoft.Research.DecisionService.Uploader;
    using MultiWorldTesting;
    using MultiWorldTesting.SingleAction;
    using Newtonsoft.Json;
    using System;
    using ClientDecisionService;

    /// <summary>
    /// Configuration object for the client decision service which contains settings for batching, retry storage, etc...
    /// </summary>
    public class DecisionServiceConfiguration<TContext> : BaseDecisionServiceConfiguration<TContext>
    {
        public DecisionServiceConfiguration(string authorizationToken, IExplorer<TContext> explorer)
            : base(authorizationToken)
        {
            if (explorer == null)
            {
                throw new ArgumentNullException("explorer", "Exploration algorithm cannot be null");
            }

            this.Explorer = explorer;
        }

        /// <summary>
        /// The <see cref="IExplorer{TContext}"/> object representing an exploration algorithm.
        /// </summary>
        public IExplorer<TContext> Explorer { get; private set; }

        #region Optional Parameters

        /// <summary>
        /// Specifies a custom <see cref="IRecorder{TContext}"/> object to be used for logging exploration data. 
        /// </summary>
        public IRecorder<TContext> Recorder 
        { 
            get { return recorder; } 
            set 
            { 
                if (value == null) throw new ArgumentNullException("Recorder cannot be null");
                recorder = value;
            } 
        }

        private IRecorder<TContext> recorder;

        #endregion
    }
}

namespace ClientDecisionService.MultiAction
{
    using Microsoft.Research.DecisionService.Uploader;
    using MultiWorldTesting;
    using MultiWorldTesting.MultiAction;
    using Newtonsoft.Json;
    using System;
    using ClientDecisionService;
    using System.Collections.Generic;

    /// <summary>
    /// Configuration object for the client decision service which contains settings for batching, retry storage, etc...
    /// This configuration allows the use of variable number of actions with action-dependent features.
    /// </summary>
    public class DecisionServiceConfiguration<TContext, TActionDependentFeature> : BaseDecisionServiceConfiguration<TContext>
    {
        public DecisionServiceConfiguration(string authorizationToken, IExplorer<TContext> explorer, Func<TContext, uint> getNumberOfActionsFunc)
            : base(authorizationToken)
        {
            if (explorer == null)
            {
                throw new ArgumentNullException("explorer", "Exploration algorithm cannot be null");
            }

            if (getNumberOfActionsFunc == null)
            {
                throw new ArgumentNullException("getNumberOfActionsFunc", "A method to retrieve number of actions from the context must be specified.");
            }

            this.Explorer = explorer;
            this.GetNumberOfActionsFunc = getNumberOfActionsFunc;
        }

        /// <summary>
        /// The <see cref="IExplorer{TContext}"/> object representing an exploration algorithm.
        /// </summary>
        public IExplorer<TContext> Explorer { get; private set; }

        #region Optional Parameters

        /// <summary>
        /// Specifies a custom <see cref="IRecorder{TContext}"/> object to be used for logging exploration data. 
        /// </summary>
        public IRecorder<TContext> Recorder
        {
            get { return recorder; }
            set
            {
                if (value == null) throw new ArgumentNullException("Recorder cannot be null");
                recorder = value;
            }
        }

        /// <summary>
        /// A callback to return the action-dependent features in the context.
        /// </summary>
        public Func<TContext, IReadOnlyCollection<TActionDependentFeature>> GetContextFeaturesFunc { get; set; }

        /// <summary>
        /// A callback to return the number of actions in the context.
        /// </summary>
        public Func<TContext, uint> GetNumberOfActionsFunc { get; set; }
        
        #endregion

        private IRecorder<TContext> recorder;
    }
}
