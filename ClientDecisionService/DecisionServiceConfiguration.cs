using Microsoft.Research.MultiWorldTesting.JoinUploader;
using System;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceConfiguration
    {
        public DecisionServiceConfiguration(string authorizationToken)
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

        /// <summary>
        ///  TODO: comment
        /// </summary>
        public Stream ModelStream { get; set; }

        #region Optional Parameters

        /// <summary>
        /// Whether the context provided is already serialized in JSON format.
        /// </summary>
        internal bool UseJsonContext { get; set; }

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
        /// Triggers when a polling for model update fails.
        /// </summary>
        public Action<Exception> ModelPollFailureCallback { get; set; }
        
        /// <summary>
        /// Triggers when a polling for settings update fails.
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
