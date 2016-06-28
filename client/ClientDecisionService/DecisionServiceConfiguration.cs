using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceConfiguration
    {
        public DecisionServiceConfiguration(string settingsBlobUri)
        {
            if (settingsBlobUri == null)
            {
                throw new ArgumentNullException("settingsBlobUri", "Settings blob Uri cannot be null");
            }

            this.SettingsBlobUri = settingsBlobUri;
            this.LogAppInsights = true;
        }

        /// <summary>
        /// The settings blob uri for this application.
        /// </summary>
        public string SettingsBlobUri { get; private set; }

        /// <summary>
        /// Optional storage connection string used in conjunction with <see cref="SettingsBlobUri"/> to pass authentication.
        /// </summary>
        public string AzureStorageConnectionString { get; set; }

        /// <summary>
        ///  TODO: comment
        /// </summary>
        public Stream ModelStream { get; set; }

        #region Optional Parameters

        /// <summary>
        /// Whether to log diagnostic trace messages to Application Insights associated with the application.
        /// </summary>
        public bool LogAppInsights { get; set; }

        /// <summary>
        /// In development mode, more diagnostics messages such as example contexts will be logged.
        /// Batch upload is turned off by default but honors user-specified configuration.
        /// </summary>
        public bool DevelopmentMode { get; set; }

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

        public string OfflineApplicationID { get; set; }

        /// <summary>
        /// Specifies the batching configuration when uploading interaction data to join service.
        /// </summary>
        /// <remarks>
        /// In offline mode, batching configuration will not be used since the join service recorder is turned off.
        /// </remarks>
        public BatchingConfiguration InteractionUploadConfiguration
        {
            get { return batchConfig; }
            set
            {
                if (value == null) throw new ArgumentNullException("Interaction upload configuration cannot be null");
                batchConfig = value;
            }
        }

        /// <summary>
        /// Specifies the batching configuration when uploading observation data to join service.
        /// </summary>
        /// <remarks>
        /// In offline mode, batching configuration will not be used since the join service recorder is turned off.
        /// </remarks>
        public BatchingConfiguration ObservationUploadConfiguration
        {
            get { return batchConfig; }
            set
            {
                if (value == null) throw new ArgumentNullException("Observation upload configuration cannot be null");
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

        public Action<byte[]> ModelPollSuccessCallback { get; set; }

        public Action<byte[]> SettingsPollSuccessCallback { get; set; }

        #endregion

        private BatchingConfiguration batchConfig;
        private string loggingServiceAddress;
        private TimeSpan pollingForSettingsPeriod;
        private TimeSpan pollingForModelPeriod;
    }
}
