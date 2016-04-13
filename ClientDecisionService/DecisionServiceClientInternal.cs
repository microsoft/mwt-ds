using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal sealed class DecisionServiceClientInternal<TContext, TAction, TPolicyValue> : IDisposable, IModelSender
    {
        private readonly TimeSpan settingsBlobPollDelay;

        private readonly string updateSettingsTaskId = "settings";

        private readonly IRecorder<TContext, TAction> recorder;
        private readonly ILogger logger;
        private readonly IContextMapper<TContext, TPolicyValue> internalPolicy;

        private readonly DecisionServiceConfiguration config;
        private readonly ApplicationTransferMetadata metaData;
        private MwtExplorer<TContext, TAction, TPolicyValue> mwtExplorer;

        private event EventHandler<Stream> sendModelHandler;
        event EventHandler<Stream> IModelSender.Send
        {
            add { this.sendModelHandler += value; }
            remove { this.sendModelHandler -= value; }
        }

        public DecisionServiceClientInternal(
            DecisionServiceConfiguration config,
            ApplicationTransferMetadata metaData,
            IExplorer<TAction, TPolicyValue> explorer,
            IContextMapper<TContext, TPolicyValue> internalPolicy,
            IContextMapper<TContext, TPolicyValue> initialPolicy = null,
            IFullExplorer<TAction> initialExplorer = null,
            IRecorder<TContext, TAction> recorder = null)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (explorer == null)
                throw new ArgumentNullException("explorer");

            this.config = config;
            this.metaData = metaData;

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
                throw new Exception("Initial Explorer and Default Policy are both specified but only one can be used.");
            }
            INumberOfActionsProvider<TContext> numActionsProvider = null;
            if (initialExplorer != null) // only needed when full exploration
            {
                numActionsProvider = internalPolicy as INumberOfActionsProvider<TContext>;
                if (numActionsProvider == null)
	            {
                    var dsPolicy = internalPolicy as DecisionServicePolicy<TContext, TAction>;
                    numActionsProvider = (dsPolicy != null) ? dsPolicy.NumActionsProvider : null;
	            }
                if (numActionsProvider == null)
                {
                    numActionsProvider = explorer as INumberOfActionsProvider<TContext>;
                }
                if (numActionsProvider == null)
                {
                    throw new ArgumentException("Explorer must implement INumberOfActionsProvider interface");
                }
            }

            this.mwtExplorer = MwtExplorer.Create(config.AuthorizationToken,
                this.recorder, explorer, initialExplorer, numActionsProvider);
            this.mwtExplorer.Policy = initialPolicy;
        }

        internal async Task DownloadModelAndUpdate(CancellationToken cancellationToken)
        {
            var modelMetadata = new AzureBlobUpdateMetadata(
               "model", metadata.ModelBlobUri,
               metadata.ConnectionString,
               config.BlobOutputDir, TimeSpan.MinValue,
               modelFile =>
               {
                   using (var modelStream = File.OpenRead(modelFile))
                   {
                       UpdateModel(modelStream);
                       Trace.TraceInformation("Model download succeeded.");
                   }
               },
               null,
               cancellationToken);

            await AzureBlobUpdateTask.DownloadAsync(modelMetadata);
        }

        internal IExplorer<TAction, TPolicyValue> Explorer
        {
            get { return this.mwtExplorer.Explorer; }
            set { this.mwtExplorer.Explorer = value; }
        }

        internal IFullExplorer<TAction> InitialExplorer
        {
            get { return this.mwtExplorer.InitialExplorer; }
            set { this.mwtExplorer.InitialExplorer = value; }
        }

        /// <summary>
        /// Report a simple float reward for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="reward">The simple float reward.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        internal void ReportReward(float reward, UniqueEventID uniqueKey)
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
        internal void ReportOutcome(object outcome, UniqueEventID uniqueKey)
        {
            if (this.logger != null)
                logger.ReportOutcome(uniqueKey, outcome);
        }

        /// <summary>
        /// TODO: Stream needs to be disposed by users
        /// </summary>
        /// <param name="model"></param>
        internal void UpdateModel(Stream model)
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
        internal void Flush()
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
            catch (JsonReaderException jrex)
            {
                Trace.TraceWarning("Cannot read new settings: " + jrex.Message);
            }
        }
    }
}
