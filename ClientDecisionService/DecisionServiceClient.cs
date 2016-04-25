using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public sealed class DecisionServiceClient<TContext, TAction, TPolicyValue> : IDisposable
    {
        private readonly string updateSettingsTaskId = "settings";
        private readonly string updateModelTaskId = "model";

        private readonly IContextMapper<TContext, TPolicyValue> internalPolicy;

        private IRecorder<TContext, TAction> recorder;
        private ILogger logger;
        private IContextMapper<TContext, TAction> initialPolicy;

        private readonly DecisionServiceConfiguration config;
        private readonly ApplicationTransferMetadata metaData;
        private MwtExplorer<TContext, TAction, TPolicyValue> mwtExplorer;

        private class OfflineRecorder : IRecorder<TContext, TAction>
        {
            public void Record(TContext context, TAction value, object explorerState, object mapperState, UniqueEventID uniqueKey)
            {
                throw new NotSupportedException("Must set an recorder in offline mode");
            }
        }

        public DecisionServiceClient(
            DecisionServiceConfiguration config,
            ApplicationTransferMetadata metaData,
            IExplorer<TAction, TPolicyValue> explorer,
            IContextMapper<TContext, TPolicyValue> internalPolicy,
            IContextMapper<TContext, TAction> initialPolicy = null,
            IFullExplorer<TAction> initialExplorer = null,
            int? numActions = null)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (explorer == null)
                throw new ArgumentNullException("explorer");

            this.config = config;

            if (config.OfflineMode)
                this.recorder = new OfflineRecorder();
            else
            {
                this.metaData = metaData;

                if (metaData == null)
                    throw new Exception("Unable to locate a registered MWT application.");

                if (this.recorder == null)
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
                                metaData.EventHubConnectionString,
                                metaData.EventHubInputName,
                                config.JoinServiceBatchConfiguration);
                            break;
                    }
                    this.recorder = (IRecorder<TContext, TAction>)joinServerLogger;
                }

                var settingsBlobPollDelay = config.PollingForSettingsPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForSettingsPeriod;
                if (settingsBlobPollDelay != TimeSpan.MinValue)
                {
                    AzureBlobUpdater.RegisterTask(
                        this.updateSettingsTaskId,
                        metaData.SettingsBlobUri,
                        metaData.ConnectionString,
                        config.BlobOutputDir,
                        settingsBlobPollDelay,
                        this.UpdateSettings,
                        config.SettingsPollFailureCallback);
                }

                var modelBlobPollDelay = config.PollingForModelPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForModelPeriod;
                if (modelBlobPollDelay != TimeSpan.MinValue)
                {
                    AzureBlobUpdater.RegisterTask(
                        this.updateModelTaskId,
                        metaData.ModelBlobUri,
                        metaData.ConnectionString,
                        config.BlobOutputDir, 
                        modelBlobPollDelay,
                        this.UpdateContextMapperFromFile,
                        config.ModelPollFailureCallback);
                }

                AzureBlobUpdater.Start();
            }

            this.logger = this.recorder as ILogger;
            this.internalPolicy = internalPolicy;
            this.initialPolicy = initialPolicy;

            if (initialExplorer != null && initialPolicy != null)
                throw new Exception("Initial Explorer and Default Policy are both specified but only one can be used.");

            if (numActions == null)
            {
                INumberOfActionsProvider<TContext> numActionsProvider = null;
                if (initialExplorer != null) // only needed when full exploration
                {
                    numActionsProvider = internalPolicy as INumberOfActionsProvider<TContext>;
                    if (numActionsProvider == null)
                        numActionsProvider = explorer as INumberOfActionsProvider<TContext>;

                    if (numActionsProvider == null)
                        throw new ArgumentException("Explorer must implement INumberOfActionsProvider interface");
                }

                this.mwtExplorer = MwtExplorer.Create(config.AuthorizationToken,
                    numActionsProvider, this.recorder, explorer, initialExplorer: initialExplorer);
            }
            else
            {
                this.mwtExplorer = MwtExplorer.Create(config.AuthorizationToken,
                    (int)numActions, this.recorder, explorer, initialExplorer: initialExplorer);
            }
        }

        private void UpdateContextMapperFromFile(string modelFile)
        {
            var updatable = this.internalPolicy as IUpdatable<Stream>;
            if (updatable != null)
            {
                using (var stream = File.OpenRead(modelFile))
                {
                    updatable.Update(stream);

                    Trace.TraceInformation("Model update succeeded.");
                }
            }
        }

        public async Task DownloadModelAndUpdate(CancellationToken cancellationToken)
        {
            bool modelFound = false;
            var modelMetadata = new AzureBlobUpdateMetadata(
               "model", this.metaData.ModelBlobUri,
               this.metaData.ConnectionString,
               config.BlobOutputDir, TimeSpan.MinValue,
               modelFile =>
               {
                   using (var modelStream = File.OpenRead(modelFile))
                   {
                       UpdateModel(modelStream);
                       modelFound = true;
                       Trace.TraceInformation("Model download succeeded.");
                   }
               },
               null,
               cancellationToken);

            await AzureBlobUpdateTask.DownloadAsync(modelMetadata);

            if (!modelFound)
            {
                throw new ModelNotFoundException("No MWT model found for download.");
            }
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

        internal IContextMapper<TContext, TAction> InitialPolicy
        {
            get { return this.initialPolicy; }
            set { this.initialPolicy = value; }
        }

        public IRecorder<TContext, TAction> Recorder
        {
            get { return this.recorder; }
            set 
            {
                if (value == null)
                    throw new ArgumentNullException("Recorder");

                this.recorder = value;
                this.logger = value as ILogger;
                this.mwtExplorer.Recorder = value;
            }
        }

        public DecisionServiceClient<TContext, TAction, TPolicyValue> WithRecorder(IRecorder<TContext, TAction> recorder)
        {
            this.Recorder = recorder;
            return this;
        }

        public TAction ChooseAction(UniqueEventID uniqueKey, TContext context, TAction defaultAction)
        {
            return this.mwtExplorer.ChooseAction(uniqueKey, context, defaultAction);
        }

        public TAction ChooseAction(UniqueEventID uniqueKey, TContext context)
        {
            var initialPolicy = this.initialPolicy;
            if (initialPolicy != null)
            {
                var defaultAction = initialPolicy.MapContext(context).Value;
                return this.mwtExplorer.ChooseAction(uniqueKey, context, defaultAction);
            }

            return this.mwtExplorer.ChooseAction(uniqueKey, context);
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
            var updatable = this.internalPolicy as IUpdatable<Stream>;
            if (updatable != null)
            {
                updatable.Update(model);

                // Swap out initial policy and use the internal policy to handle new model
                this.mwtExplorer.Policy = this.internalPolicy;
            }
        }

        public void Dispose()
        {
            // stops all updates
            AzureBlobUpdater.Stop();

            if (this.recorder != null)
            {
                // Flush any pending data to be logged 
                var disposable = this.recorder as IDisposable;
                if (disposable != null)
                    disposable.Dispose();

                recorder = null;
            }

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
