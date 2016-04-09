using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Factory class.
    /// </summary>
    public static class DecisionServiceClient
    {
        public static DecisionServiceClient<TContext, TValue, TMapperValue>
        Create<TContext, TValue, TMapperValue>(
            ExploreConfigurationWrapper<TContext, TValue, TMapperValue> explorer,
            IRecorder<TContext, TValue> recorder = null)
        {
            var dsClient = new DecisionServiceClient<TContext, TValue, TMapperValue>(
                explorer.ContextMapper.Configuration, explorer.ContextMapper.Metadata, explorer.Explorer, recorder);
            explorer.Subscribe(dsClient);
            return dsClient;
        }
    }

    public class DecisionServiceClient<TContext, TValue, TMapperValue> : IDisposable, IModelSender
    {
        private readonly TimeSpan settingsBlobPollDelay;

        private readonly string updateSettingsTaskId = "settings";

        private readonly IRecorder<TContext, TValue> recorder;
        private readonly ILogger logger;

        protected readonly DecisionServiceConfiguration config;
        protected MwtExplorer<TContext, TValue, TMapperValue> mwtExplorer;

        private event EventHandler<Stream> sendModelHandler;
        event EventHandler<Stream> IModelSender.Send
        {
            add { this.sendModelHandler += value; }
            remove { this.sendModelHandler -= value; }
        }

        public DecisionServiceClient(
            DecisionServiceConfiguration config,
            ApplicationTransferMetadata metaData,
            IExplorer<TValue, TMapperValue> explorer,
            IFullExplorer<TContext, TValue> initialExplorer = null,
            IRecorder<TContext, TValue> recorder = null)
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
                    var joinServerLogger = new JoinServiceLogger<TContext, TValue>();
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
                    this.recorder = (IRecorder<TContext, TValue>)joinServerLogger;
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
            this.mwtExplorer = MwtExplorer.Create(config.AuthorizationToken, this.recorder, explorer, initialExplorer);
        }

        public TValue ChooseAction(UniqueEventID uniqueKey, TContext context)
        {
            return this.mwtExplorer.ChooseAction(uniqueKey, context);
        }

        public TValue ChooseAction(UniqueEventID uniqueKey, TContext context, TValue initialAction)
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
                sendModelHandler(this, model);
            }
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
