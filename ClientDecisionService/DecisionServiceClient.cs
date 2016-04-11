using Microsoft.Research.MultiWorldTesting.ClientLibrary.VW;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Factory class.
    /// </summary>
    public static class DecisionServiceClient
    {
        public static DecisionServiceClient<TContext, TAction, TPolicyValue>
        Create<TContext, TAction, TPolicyValue>(
            ExploreConfigurationWrapper<TContext, TAction, TPolicyValue> explorer,
            IRecorder<TContext, TAction> recorder = null)
        {
            var dsClient = new DecisionServiceClient<TContext, TAction, TPolicyValue>(
                explorer.ContextMapper.Configuration,
                explorer.ContextMapper.Metadata,
                explorer.Explorer,
                explorer.ContextMapper.InternalPolicy,
                initialExplorer: explorer.InitialFullExplorer,
                initialPolicy: explorer.ContextMapper.InitialPolicy,
                recorder: recorder);
            explorer.Subscribe(dsClient);
            return dsClient;
        }

        public static DecisionServiceConfigurationWrapper<string, int> WithJsonPolicy(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = true;
            return DecisionServiceClient.Wrap(config, new VWJsonPolicy(config.ModelStream));
        }

        public static DecisionServiceConfigurationWrapper<string, int[]> WithJsonRanker(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = true;
            return DecisionServiceClient.Wrap(config, new VWJsonRanker(config.ModelStream));
        }

        public static DecisionServiceConfigurationWrapper<TContext, int> WithPolicy<TContext>(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = false;
            return DecisionServiceClient.Wrap(config, new VWPolicy<TContext>(config.ModelStream, config.FeatureDiscovery));
        }

        public static DecisionServiceConfigurationWrapper<TContext, int[]> WithRanker<TContext>(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = false;
            return DecisionServiceClient.Wrap(config, new VWRanker<TContext>(config.ModelStream, config.FeatureDiscovery));
        }

        public static DecisionServiceConfigurationWrapper<TContext, int[]>
            WithRanker<TContext, TActionDependentFeature>(
                DecisionServiceConfiguration config,
                Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc)
        {
            config.UseJsonContext = false;
            return DecisionServiceClient.Wrap(config, new VWRanker<TContext, TActionDependentFeature>(getContextFeaturesFunc, config.ModelStream, config.FeatureDiscovery));
        }


        public static DecisionServiceConfigurationWrapper<TContext, TPolicyValue>
            Wrap<TContext, TPolicyValue>(
                DecisionServiceConfiguration config,
                IContextMapper<TContext, TPolicyValue> vwPolicy)
        {
            var metaData = GetBlobLocations(config);
            var ucm = new DecisionServiceConfigurationWrapper<TContext, TPolicyValue>
            {
                Configuration = config,
                Metadata = metaData
            };

            // conditionally wrap if it can be updated.
            var updatableContextMapper = vwPolicy as IUpdatable<Stream>;

            IContextMapper<TContext, TPolicyValue> policy;

            if (config.OfflineMode || metaData == null || updatableContextMapper == null)
                policy = vwPolicy;
            else
            {
                var dsPolicy = new DecisionServicePolicy<TContext, TPolicyValue>(vwPolicy, config, metaData);
                dsPolicy.Subscribe(ucm);
                policy = dsPolicy;
            }
            ucm.InternalPolicy = policy;

            return ucm;
        }

        internal static ApplicationTransferMetadata GetBlobLocations(DecisionServiceConfiguration config)
        {
            if (config.OfflineMode)
                return null;

            string redirectionBlobLocation = string.Format(DecisionServiceConstants.RedirectionBlobLocation, config.AuthorizationToken);

            try
            {
                using (var wc = new WebClient())
                {
                    string jsonMetadata = wc.DownloadString(redirectionBlobLocation);
                    return JsonConvert.DeserializeObject<ApplicationTransferMetadata>(jsonMetadata);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Unable to retrieve blob locations from storage using the specified token", ex);
            }
        }
    }

    public class DecisionServiceClient<TContext, TAction, TPolicyValue> : IDisposable, IModelSender
    {
        private readonly TimeSpan settingsBlobPollDelay;

        private readonly string updateSettingsTaskId = "settings";

        private readonly IRecorder<TContext, TAction> recorder;
        private readonly ILogger logger;
        private readonly IContextMapper<TContext, TPolicyValue> internalPolicy;

        protected readonly DecisionServiceConfiguration config;
        protected MwtExplorer<TContext, TAction, TPolicyValue> mwtExplorer;

        private event EventHandler<Stream> sendModelHandler;
        event EventHandler<Stream> IModelSender.Send
        {
            add { this.sendModelHandler += value; }
            remove { this.sendModelHandler -= value; }
        }

        public DecisionServiceClient(
            DecisionServiceConfiguration config,
            ApplicationTransferMetadata metaData,
            IExplorer<TAction, TPolicyValue> explorer,
            IContextMapper<TContext, TPolicyValue> internalPolicy,
            IContextMapper<TContext, TPolicyValue> initialPolicy = null,
            IFullExplorer<TContext, TAction> initialExplorer = null,
            IRecorder<TContext, TAction> recorder = null)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (explorer == null)
                throw new ArgumentNullException("explorer");

            this.config = config;

            if (config.OfflineMode)
            {
                if (recorder == null)
                    throw new ArgumentException("A custom recorder must be defined when operating in offline mode.", "Recorder");

                this.recorder = recorder;
            }
            else
            {
                if (metaData == null)
                {
                    throw new Exception("Unable to locate a registered MWT application.");
                }

                if (recorder != null)
                {
                    this.recorder = recorder;
                }
                else
                {
                    var joinServerLogger = new JoinServiceLogger<TContext, TAction>();
                    switch (config.JoinServerType)
                    {
                        case JoinServerType.CustomSolution:
                            joinServerLogger.InitializeWithCustomAzureJoinServer(
                                config.AuthorizationToken,
                                config.LoggingServiceAddress,
                                config.JoinServiceBatchConfiguration);
                            break;
                        case JoinServerType.AzureStreamAnalytics:
                        default:
                            joinServerLogger.InitializeWithAzureStreamAnalyticsJoinServer(
                                config.EventHubConnectionString,
                                config.EventHubInputName,
                                config.JoinServiceBatchConfiguration);
                            break;
                    }
                    this.recorder = (IRecorder<TContext, TAction>)joinServerLogger;
                }

                this.settingsBlobPollDelay = config.PollingForSettingsPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForSettingsPeriod;

                if (this.settingsBlobPollDelay != TimeSpan.MinValue)
                {
                    AzureBlobUpdater.RegisterTask(
                        this.updateSettingsTaskId,
                        metaData.SettingsBlobUri,
                        metaData.ConnectionString,
                        config.BlobOutputDir,
                        this.settingsBlobPollDelay,
                        this.UpdateSettings,
                        config.SettingsPollFailureCallback);
                }
                AzureBlobUpdater.Start();
            }

            this.logger = this.recorder as ILogger;
            this.internalPolicy = internalPolicy;

            if (initialExplorer != null && initialPolicy != null)
            {
                // TODO: raise warning and use default policy (safer) instead of throwing exception?
                throw new Exception("Initial Explorer and Default Policy are both specified but only one can be used.");
            }
            this.mwtExplorer = MwtExplorer.Create(config.AuthorizationToken, this.recorder, explorer, initialExplorer);
            this.mwtExplorer.Policy = initialPolicy;
        }

        public TAction ChooseAction(UniqueEventID uniqueKey, TContext context)
        {
            return this.mwtExplorer.ChooseAction(uniqueKey, context);
        }

        public TAction ChooseAction(UniqueEventID uniqueKey, TContext context, TAction initialAction)
        {
            return this.mwtExplorer.ChooseAction(uniqueKey, context, initialAction);
        }

        /// <summary>
        /// Report a simple float reward for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="reward">The simple float reward.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        public void ReportReward(float reward, UniqueEventID uniqueKey)
        {
            if (this.logger != null)
                this.logger.ReportReward(uniqueKey, reward);
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
            if (this.logger != null)
                logger.ReportOutcome(uniqueKey, outcome);
        }

        /// <summary>
        /// TODO: Stream needs to be disposed by users
        /// </summary>
        /// <param name="model"></param>
        public void UpdateModel(Stream model)
        {
            if (sendModelHandler != null)
            {
                // Raise update model event so that any subscribers can update appropriately
                // For example, the internal VW policy needs to change its model
                sendModelHandler(this, model);
            }
            // Swap out initial policy and use the internal policy to handle new model
            this.mwtExplorer.Policy = this.internalPolicy;
        }

        /// <summary>
        /// Flush any pending data to be logged and request to stop all polling as appropriate.
        /// </summary>
        public void Flush()
        {
            // stops all updates
            AzureBlobUpdater.Stop();

            if (this.logger != null)
                logger.Flush();
        }

        public void Dispose()
        {
            this.Flush();

            if (this.mwtExplorer != null)
            {
                this.mwtExplorer.Dispose();
                this.mwtExplorer = null;
            }
        }

        private void UpdateSettings(string settingsFile)
        {
            try
            {
                string metadataJson = File.ReadAllText(settingsFile);
                var metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(metadataJson);

                // TODO: not sure if we want to bypass or expose EnableExplore in MWT explorer?
                this.mwtExplorer.Explorer.EnableExplore(metadata.IsExplorationEnabled);
            }
            catch(JsonReaderException jrex)
            {
                Trace.TraceWarning("Cannot read new settings: " + jrex.Message);
            }
        }
    }
}
