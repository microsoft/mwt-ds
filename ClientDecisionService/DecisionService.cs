namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction
{
    using MultiWorldTesting.Contract;
    using MultiWorldTesting.ExploreLibrary;
    using MultiWorldTesting.ExploreLibrary.SingleAction;
    using Newtonsoft.Json;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;

    /// <summary>
    /// Encapsulates logic for recorder with async server communications & policy update.
    /// </summary>
    public class DecisionService<TContext> : IDisposable
    {
        /// <summary>
        /// Construct a <see cref="DecisionService{TContext}"/> object with the specified <see cref="DecisionServiceConfiguration{TContext}"/> configuration.
        /// </summary>
        /// <param name="config">The configuration object.</param>
        public DecisionService(DecisionServiceConfiguration<TContext> config)
        {
            explorer = config.Explorer;

            if (!config.OfflineMode)
            {
                if (config.Recorder == null)
                {
                    var joinServerLogger = new JoinServiceLogger<TContext>();
                    switch (config.JoinServerType)
                    {
                        case JoinServerType.CustomAzureSolution:
                            joinServerLogger.InitializeWithCustomAzureJoinServer(
                                config.AuthorizationToken,
                                config.LoggingServiceAddress,
                                config.JoinServiceBatchConfiguration);
                            break;
                        case JoinServerType.AzureStreamAnalytics:
                            joinServerLogger.InitializeWithAzureStreamAnalyticsJoinServer(
                                config.EventHubConnectionString,
                                config.EventHubInputName,
                                config.JoinServiceBatchConfiguration);
                            break;
                    }
                    this.recorder = joinServerLogger;
                }
                else
                {
                    this.recorder = config.Recorder;
                }

                this.settingsBlobPollDelay = config.PollingForSettingsPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForSettingsPeriod;
                this.modelBlobPollDelay = config.PollingForModelPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForModelPeriod;

                if (this.settingsBlobPollDelay != TimeSpan.MinValue || this.modelBlobPollDelay != TimeSpan.MinValue)
                {
                    string redirectionBlobLocation = string.Format(DecisionServiceConstants.RedirectionBlobLocation, config.AuthorizationToken);
                    ApplicationTransferMetadata metadata = this.GetBlobLocations(config.AuthorizationToken, redirectionBlobLocation);

                    if (this.settingsBlobPollDelay != TimeSpan.MinValue)
                    {
                        this.blobUpdater = new AzureBlobUpdater(
                            "settings",
                            metadata.SettingsBlobUri,
                            metadata.ConnectionString,
                            config.BlobOutputDir,
                            this.settingsBlobPollDelay,
                            this.UpdateSettings,
                            config.SettingsPollFailureCallback);
                    }

                    if (this.modelBlobPollDelay != TimeSpan.MinValue)
                    {
                        this.policy = new DecisionServicePolicy<TContext>(
                            metadata.ModelBlobUri, metadata.ConnectionString,
                            config.BlobOutputDir,
                            this.modelBlobPollDelay,
                            config.SetModelIdCallback,
                            this.InternalPolicyUpdated,
                            config.ModelPollFailureCallback);
                    }
                }
            }
            else
            {
                this.recorder = config.Recorder;
                if (this.recorder == null)
                {
                    throw new ArgumentException("A custom recorder must be defined when operating in offline mode.", "Recorder");
                }
            }

            mwt = new MwtExplorer<TContext>(config.AuthorizationToken, this.recorder);
        }

        /// <summary>
        /// Report a simple float reward for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="reward">The simple float reward.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        public void ReportReward(float reward, UniqueEventID uniqueKey)
        {
            ILogger<TContext> logger = this.recorder as ILogger<TContext>;
            if (logger != null)
            {
                logger.ReportReward(uniqueKey, reward);
            }
        }

        /// <summary>
        /// Report an outcome in JSON format for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="outcomeJson">The outcome object in JSON format.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        /// <remarks>
        /// Outcomes are general forms of observations that can be converted to simple float rewards as required by some ML algorithms for optimization.
        /// </remarks>
        public void ReportOutcome(object outcome, UniqueEventID uniqueKey)
        {
            ILogger<TContext> logger = this.recorder as ILogger<TContext>;
            if (logger != null)
            {
                logger.ReportOutcome(uniqueKey, outcome);
            }
        }

        /// <summary>
        /// Performs explore-exploit to choose an action based on the specified context.
        /// </summary>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        /// <param name="context">The context for this interaction.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>uint</returns>
        /// <remarks>
        /// This method will send logging data to the <see cref="IRecorder{TContext}"/> object specified at initialization.
        /// </remarks>
        public uint ChooseAction(UniqueEventID uniqueKey, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            return mwt.ChooseAction(explorer, uniqueKey, context, numActionsVariable);
        }

        /// <summary>
        /// Update decision service with a new <see cref="IPolicy{TContext}"/> object that will be used by the exploration algorithm.
        /// </summary>
        /// <param name="newPolicy">The new <see cref="IPolicy{TContext}"/> object.</param>
        public void UpdatePolicy(IPolicy<TContext> newPolicy)
        {
            UpdateInternalPolicy(newPolicy);
        }

        /// <summary>
        /// Flush any pending data to be logged and request to stop all polling as appropriate.
        /// </summary>
        public void Flush()
        {
            if (blobUpdater != null)
            {
                blobUpdater.StopPolling();
            }

            if (policy != null)
            {
                policy.StopPolling();
            }

            ILogger<TContext> logger = this.recorder as ILogger<TContext>;
            if (logger != null)
            {
                logger.Flush();
            }
        }

        public void Dispose() { }

        private ApplicationTransferMetadata GetBlobLocations(string token, string redirectionBlobLocation)
        {
            ApplicationTransferMetadata metadata = null;

            try
            {
                using (var wc = new WebClient())
                {
                    string jsonMetadata = wc.DownloadString(redirectionBlobLocation);
                    metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(jsonMetadata);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Unable to retrieve blob locations from storage using the specified token", ex);
            }
            return metadata;
        }

        private void UpdateSettings(string settingsFile)
        {
            try
            {
                string metadataJson = File.ReadAllText(settingsFile);
                var metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(metadataJson);

                this.explorer.EnableExplore(metadata.IsExplorationEnabled);
            }
            catch (Exception ex)
            {
                if (ex is JsonReaderException)
                {
                    Trace.TraceWarning("Cannot read new settings.");
                }
                else
                {
                    throw;
                }
            }
            
        }

        private void InternalPolicyUpdated()
        {
            UpdateInternalPolicy(policy);
        }

        private void UpdateInternalPolicy(IPolicy<TContext> newPolicy)
        {
            IConsumePolicy<TContext> consumePolicy = explorer as IConsumePolicy<TContext>;
            if (consumePolicy != null)
            {
                consumePolicy.UpdatePolicy(newPolicy);
                Trace.TraceInformation("Model update succeeded.");
            }
            else
            {
                // TODO: how to handle updating policies for Bootstrap explorers?
                throw new NotSupportedException("This type of explorer does not currently support updating policy functions.");
            }
        }

        public IRecorder<TContext> Recorder { get { return recorder; } }
        public IPolicy<TContext> Policy { get { return policy; } }

        private readonly TimeSpan settingsBlobPollDelay;
        private readonly TimeSpan modelBlobPollDelay;

        AzureBlobUpdater blobUpdater;

        private readonly IExplorer<TContext> explorer;
        private readonly IRecorder<TContext> recorder;
        private readonly DecisionServicePolicy<TContext> policy;
        private readonly MwtExplorer<TContext> mwt;
    }
}

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction
{
    using Microsoft.Research.MultiWorldTesting.Contract;
    using MultiWorldTesting.ExploreLibrary;
    using MultiWorldTesting.ExploreLibrary.MultiAction;
    using Newtonsoft.Json;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;

    /// <summary>
    /// Encapsulates logic for recorder with async server communications & policy update.
    /// </summary>
    public class DecisionService<TContext, TActionDependentFeature> : IDisposable
    {
        /// <summary>
        /// Construct a <see cref="DecisionService{TContext, TActionDependentFeature}"/> object with the specified <see cref="DecisionServiceConfiguration{TContext, TActionDependentFeature}"/> configuration.
        /// </summary>
        /// <param name="config">The configuration object.</param>
        public DecisionService(DecisionServiceConfiguration<TContext, TActionDependentFeature> config)
        {
            explorer = config.Explorer;

            if (!config.OfflineMode)
            {
                if (config.Recorder == null)
                {
                    var joinServerLogger = new JoinServiceLogger<TContext>();
                    switch (config.JoinServerType)
                    {
                        case JoinServerType.CustomAzureSolution:
                            joinServerLogger.InitializeWithCustomAzureJoinServer(
                                config.AuthorizationToken,
                                config.LoggingServiceAddress,
                                config.JoinServiceBatchConfiguration);
                            break;
                        case JoinServerType.AzureStreamAnalytics:
                            joinServerLogger.InitializeWithAzureStreamAnalyticsJoinServer(
                                config.EventHubConnectionString,
                                config.EventHubInputName,
                                config.JoinServiceBatchConfiguration);
                            break;
                    }
                    this.recorder = joinServerLogger;
                }
                else
                {
                    this.recorder = config.Recorder;
                }

                this.settingsBlobPollDelay = config.PollingForSettingsPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForSettingsPeriod;
                this.modelBlobPollDelay = config.PollingForModelPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForModelPeriod;

                if (this.settingsBlobPollDelay != TimeSpan.MinValue || this.modelBlobPollDelay != TimeSpan.MinValue)
                {
                    string redirectionBlobLocation = string.Format(DecisionServiceConstants.RedirectionBlobLocation, config.AuthorizationToken);
                    ApplicationTransferMetadata metadata = this.GetBlobLocations(config.AuthorizationToken, redirectionBlobLocation);

                    if (this.settingsBlobPollDelay != TimeSpan.MinValue)
                    {
                        this.blobUpdater = new AzureBlobUpdater(
                            "settings",
                            metadata.SettingsBlobUri,
                            metadata.ConnectionString,
                            config.BlobOutputDir,
                            this.settingsBlobPollDelay,
                            this.UpdateSettings,
                            config.SettingsPollFailureCallback);
                    }

                    if (this.modelBlobPollDelay != TimeSpan.MinValue)
                    {
                        this.policy = new DecisionServicePolicy<TContext, TActionDependentFeature>(
                            metadata.ModelBlobUri, metadata.ConnectionString,
                            config.BlobOutputDir,
                            this.modelBlobPollDelay,
                            config.GetContextFeaturesFunc,
                            config.SetModelIdCallback,
                            this.InternalPolicyUpdated,
                            config.ModelPollFailureCallback);
                    }
                }
            }
            else
            {
                this.recorder = config.Recorder;
                if (this.recorder == null)
                {
                    throw new ArgumentException("A custom recorder must be defined when operating in offline mode.", "Recorder");
                }
            }

            mwt = new MwtExplorer<TContext>(config.AuthorizationToken, this.recorder);
        }

        /// <summary>
        /// Report a simple float reward for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="reward">The simple float reward.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        public void ReportReward(float reward, UniqueEventID uniqueKey)
        {
            ILogger<TContext> logger = this.recorder as ILogger<TContext>;
            if (logger != null)
            {
                logger.ReportReward(uniqueKey, reward);
            }
        }

        /// <summary>
        /// Report an outcome in JSON format for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="outcomeJson">The outcome object in JSON format.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        /// <remarks>
        /// Outcomes are general forms of observations that can be converted to simple float rewards as required by some ML algorithms for optimization.
        /// </remarks>
        public void ReportOutcome(object outcome, UniqueEventID uniqueKey)
        {
            ILogger<TContext> logger = this.recorder as ILogger<TContext>;
            if (logger != null)
            {
                logger.ReportOutcome(uniqueKey, outcome);
            }
        }

        /// <summary>
        /// Performs explore-exploit to choose a list of actions based on the specified context.
        /// </summary>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        /// <param name="context">The context for this interaction.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An array containing the chosen actions.</returns>
        /// <remarks>
        /// This method will send logging data to the <see cref="IRecorder{TContext}"/> object specified at initialization.
        /// </remarks>
        public uint[] ChooseAction(UniqueEventID uniqueKey, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            return mwt.ChooseAction(explorer, uniqueKey, context, numActionsVariable);
        }

        /// <summary>
        /// Update decision service with a new <see cref="IPolicy{TContext}"/> object that will be used by the exploration algorithm.
        /// </summary>
        /// <param name="newPolicy">The new <see cref="IPolicy{TContext}"/> object.</param>
        public void UpdatePolicy(IPolicy<TContext> newPolicy)
        {
            UpdateInternalPolicy(newPolicy);
        }

        /// <summary>
        /// Flush any pending data to be logged and request to stop all polling as appropriate.
        /// </summary>
        public void Flush()
        {
            if (blobUpdater != null)
            {
                blobUpdater.StopPolling();
            }

            if (policy != null)
            {
                policy.StopPolling();
            }

            ILogger<TContext> logger = this.recorder as ILogger<TContext>;
            if (logger != null)
            {
                logger.Flush();
            }
        }

        public void Dispose() { }

        private ApplicationTransferMetadata GetBlobLocations(string token, string redirectionBlobLocation)
        {
            ApplicationTransferMetadata metadata = null;

            try
            {
                using (var wc = new WebClient())
                {
                    string jsonMetadata = wc.DownloadString(redirectionBlobLocation);
                    metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(jsonMetadata);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Unable to retrieve blob locations from storage using the specified token", ex);
            }
            return metadata;
        }

        private void UpdateSettings(string settingsFile)
        {
            try
            {
                string metadataJson = File.ReadAllText(settingsFile);
                var metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(metadataJson);

                this.explorer.EnableExplore(metadata.IsExplorationEnabled);
            }
            catch (Exception ex)
            {
                if (ex is JsonReaderException)
                {
                    Trace.TraceWarning("Cannot read new settings.");
                }
                else
                {
                    throw;
                }
            }

        }

        private void InternalPolicyUpdated()
        {
            UpdateInternalPolicy(policy);
        }

        private void UpdateInternalPolicy(IPolicy<TContext> newPolicy)
        {
            IConsumePolicy<TContext> consumePolicy = explorer as IConsumePolicy<TContext>;
            if (consumePolicy != null)
            {
                consumePolicy.UpdatePolicy(newPolicy);
                Trace.TraceInformation("Model update succeeded.");
            }
            else
            {
                // TODO: how to handle updating policies for Bootstrap explorers?
                throw new NotSupportedException("This type of explorer does not currently support updating policy functions.");
            }
        }

        public IRecorder<TContext> Recorder { get { return recorder; } }
        public IPolicy<TContext> Policy { get { return policy; } }

        private readonly TimeSpan settingsBlobPollDelay;
        private readonly TimeSpan modelBlobPollDelay;

        AzureBlobUpdater blobUpdater;

        private readonly IExplorer<TContext> explorer;
        private readonly IRecorder<TContext> recorder;
        private readonly DecisionServicePolicy<TContext, TActionDependentFeature> policy;
        private readonly MwtExplorer<TContext> mwt;
    }
}
