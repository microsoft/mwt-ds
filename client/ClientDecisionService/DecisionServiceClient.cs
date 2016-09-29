using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceClient<TContext> : IDisposable
    {
        private readonly IContextMapper<TContext, ActionProbability[]> internalPolicy;

        private IRecorder<TContext, int[]> recorder;
        private ILogger logger;
        private IContextMapper<TContext, ActionProbability[]> initialPolicy;
        private readonly DecisionServiceConfiguration config;
        private readonly ApplicationClientMetadata metaData;
        private MwtExplorer<TContext, int[], ActionProbability[]> mwtExplorer;
        private AzureBlobBackgroundDownloader settingsDownloader;
        private AzureBlobBackgroundDownloader modelDownloader;
        private INumberOfActionsProvider<TContext> numActionsProvider;

        private class OfflineRecorder : IRecorder<TContext, int[]>
        {
            public void Record(TContext context, int[] value, object explorerState, object mapperState, string uniqueKey)
            {
                throw new NotSupportedException("Must set an recorder in offline mode");
            }
        }

        public DecisionServiceClient(
            DecisionServiceConfiguration config,
            ApplicationClientMetadata metaData,
            IContextMapper<TContext, ActionProbability[]> internalPolicy,
            IContextMapper<TContext, ActionProbability[]> initialPolicy = null,
            IFullExplorer<int[]> initialFullExplorer = null,
            IInitialExplorer<ActionProbability[], int[]> initialExplorer = null)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (config.InteractionUploadConfiguration == null)
                config.InteractionUploadConfiguration = new JoinUploader.BatchingConfiguration(config.DevelopmentMode);

            if (config.ObservationUploadConfiguration == null)
                config.ObservationUploadConfiguration = new JoinUploader.BatchingConfiguration(config.DevelopmentMode);

            this.config = config;
            string appId = string.Empty;
            this.metaData = metaData;

            if (config.OfflineMode)
            {
                this.recorder = new OfflineRecorder();
                if (config.OfflineApplicationID == null)
                {
                    throw new ArgumentNullException("OfflineApplicationID", "Offline Application ID must be set explicitly in offline mode.");
                }
                appId = config.OfflineApplicationID;
            }
            else
            {
                if (metaData == null)
                    throw new Exception("Unable to locate a registered MWT application.");

                if (this.recorder == null)
                {
                    var joinServerLogger = new JoinServiceLogger<TContext, int[]>(metaData.ApplicationID, config.DevelopmentMode); // TODO: check token remove
                    switch (config.JoinServerType)
                    {
                        case JoinServerType.CustomSolution:
                            joinServerLogger.InitializeWithCustomAzureJoinServer(
                                config.LoggingServiceAddress,
                                config.InteractionUploadConfiguration);
                            break;
                        case JoinServerType.AzureStreamAnalytics:
                        default:
                            joinServerLogger.InitializeWithAzureStreamAnalyticsJoinServer(
                                metaData.EventHubInteractionConnectionString,
                                metaData.EventHubObservationConnectionString,
                                config.InteractionUploadConfiguration,
                                config.ObservationUploadConfiguration);
                            break;
                    }
                    this.recorder = (IRecorder<TContext, int[]>)joinServerLogger;
                }

                var settingsBlobPollDelay = config.PollingForSettingsPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForSettingsPeriod;
                if (settingsBlobPollDelay != TimeSpan.MinValue)
                {
                    this.settingsDownloader = new AzureBlobBackgroundDownloader(config.SettingsBlobUri, settingsBlobPollDelay, downloadImmediately: false, storageConnectionString: config.AzureStorageConnectionString);
                    this.settingsDownloader.Downloaded += this.UpdateSettings;
                    this.settingsDownloader.Failed += settingsDownloader_Failed;
                }

                var modelBlobPollDelay = config.PollingForModelPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForModelPeriod;
                if (modelBlobPollDelay != TimeSpan.MinValue)
                {
                    this.modelDownloader = new AzureBlobBackgroundDownloader(metaData.ModelBlobUri, modelBlobPollDelay, downloadImmediately: true, storageConnectionString: config.AzureStorageConnectionString);
                    this.modelDownloader.Downloaded += this.UpdateContextMapper;
                    this.modelDownloader.Failed += modelDownloader_Failed;
                }

                appId = metaData.ApplicationID;
            }

            this.logger = this.recorder as ILogger;
            this.internalPolicy = internalPolicy;
            this.initialPolicy = initialPolicy;

            if (initialExplorer != null && initialPolicy != null)
                throw new Exception("Initial Explorer and Default Policy are both specified but only one can be used.");

            var explorer = new GenericTopSlotExplorer();
            // explorer used if model not ready and defaultAction provided
            if (initialExplorer == null)
                initialExplorer = new EpsilonGreedyInitialExplorer(this.metaData.InitialExplorationEpsilon);

            // explorer used if model not ready and no default action provided
            if (initialFullExplorer == null)
                initialFullExplorer = new PermutationExplorer(1);

            var match = Regex.Match(metaData.TrainArguments, @"--cb_explore\s+(?<numActions>\d+)");
            if (match.Success)
            {
                var numActions = int.Parse(match.Groups["numActions"].Value);
                this.numActionsProvider = new ConstantNumActionsProvider(numActions);

                this.mwtExplorer = MwtExplorer.Create(appId,
                    numActions, this.recorder, explorer, initialPolicy, initialFullExplorer, initialExplorer);
            }
            else
            {
                if (initialExplorer != null || metaData.InitialExplorationEpsilon == 1f) // only needed when full exploration
                {
                    numActionsProvider = internalPolicy as INumberOfActionsProvider<TContext>;
                    if (numActionsProvider == null)
                        numActionsProvider = explorer as INumberOfActionsProvider<TContext>;

                    if (numActionsProvider == null)
                        throw new ArgumentException("Explorer must implement INumberOfActionsProvider interface");
                }

                this.mwtExplorer = MwtExplorer.Create(appId,
                    numActionsProvider, this.recorder, explorer, initialPolicy, initialFullExplorer, initialExplorer);
            }
        }

        void modelDownloader_Failed(object sender, Exception e)
        {
            if (this.config.ModelPollFailureCallback != null)
                this.config.ModelPollFailureCallback(e);
        }

        void settingsDownloader_Failed(object sender, Exception e)
        {
            if (this.config.SettingsPollFailureCallback != null)
                this.config.SettingsPollFailureCallback(e);
        }

        private void UpdateSettings(object sender, byte[] data)
        {
            try
            {
                using (var reader = new JsonTextReader(new StreamReader(new MemoryStream(data))))
                {
                    var jsonSerializer = new JsonSerializer();
                    var metadata = jsonSerializer.Deserialize<ApplicationClientMetadata>(reader);

                    // TODO: not sure if we want to bypass or expose EnableExplore in MWT explorer?
                    this.mwtExplorer.Explorer.EnableExplore(metadata.IsExplorationEnabled);
                }

                if (this.config.SettingsPollSuccessCallback != null)
                    this.config.SettingsPollSuccessCallback(data);
            }
            catch (JsonReaderException jrex)
            {
                Trace.TraceWarning("Cannot read new settings: " + jrex.Message);
            }
        }

        private void UpdateContextMapper(object sender, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                Trace.TraceWarning("Empty model detected, skipping model update.");
                return;
            }
            using (var stream = new MemoryStream(data))
            {
                this.UpdateModel(stream);
            }

            if (this.config.ModelPollSuccessCallback != null)
                this.config.ModelPollSuccessCallback(data);
        }

        public async Task DownloadModelAndUpdate(CancellationToken cancellationToken)
        {
            using (var wc = new WebClient())
            {
                byte[] modelData = await wc.DownloadDataTaskAsync(this.metaData.ModelBlobUri);
                if (modelData == null || modelData.Length == 0)
                {
                    Trace.TraceWarning("Empty model detected, skipping model update.");
                    return;
                }
                using (var ms = new MemoryStream(modelData))
                {
                    ms.Position = 0;
                    this.UpdateModel(ms);
                }
            }
        }

        internal IExplorer<int[], ActionProbability[]> Explorer
        {
            get { return this.mwtExplorer.Explorer; }
            set { this.mwtExplorer.Explorer = value; }
        }

        internal IContextMapper<TContext, ActionProbability[]> InitialPolicy
        {
            get { return this.initialPolicy; }
            set { this.initialPolicy = value; }
        }

        public IRecorder<TContext, int[]> Recorder
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

        public DecisionServiceClient<TContext> WithRecorder(IRecorder<TContext, int[]> recorder)
        {
            this.Recorder = recorder;
            return this;
        }

        public int ChooseAction(string uniqueKey, TContext context, IPolicy<TContext> defaultPolicy)
        {
            return ChooseAction(uniqueKey, context, defaultPolicy.MapContext(context).Value);
        }

        public int ChooseAction(string uniqueKey, TContext context, int defaultAction)
        {
            var numActions = this.numActionsProvider.GetNumberOfActions(context);
            var actions = new int[numActions];
            actions[0] = defaultAction;
            var action = 1;
            for (int i = 1; i < numActions; i++)
            {
                if (action == defaultAction)
                    action++;

                actions[i] = action;
                action++;
            }

            return this.mwtExplorer.ChooseAction(uniqueKey, context, actions)[0];
        }

        public int ChooseAction(string uniqueKey, TContext context)
        {
            return this.ChooseRanking(uniqueKey, context)[0];
        }

        public int[] ChooseRanking(string uniqueKey, TContext context, IRanker<TContext> defaultRanker)
        {
            return ChooseRanking(uniqueKey, context, defaultRanker.MapContext(context).Value);
        }

        public int[] ChooseRanking(string uniqueKey, TContext context, int[] defaultActions)
        {
            return this.mwtExplorer.ChooseAction(uniqueKey, context, defaultActions);
        }

        public int[] ChooseRanking(string uniqueKey, TContext context)
        {
            var initialPolicy = this.initialPolicy;
            if (initialPolicy != null)
            {
                return this.mwtExplorer.ChooseAction(uniqueKey, context, initialPolicy);
            }

            return this.mwtExplorer.ChooseAction(uniqueKey, context);
        }


        /// <summary>
        /// Report a simple float reward for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="reward">The simple float reward.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        public void ReportReward(float reward, string uniqueKey)
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
        public void ReportOutcome(object outcome, string uniqueKey)
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
                
                Trace.TraceInformation("Model update succeeded.");
            }
        }

        public void Dispose()
        {
            if (this.settingsDownloader != null)
            {
                this.settingsDownloader.Dispose();
                this.settingsDownloader = null;
            }

            if (this.modelDownloader != null)
            {        
                this.modelDownloader.Dispose();
                this.modelDownloader = null;
            }

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

        private sealed class ConstantNumActionsProvider : INumberOfActionsProvider<TContext>
        {
            private int numActions;

            internal ConstantNumActionsProvider(int numActions)
            {
                this.numActions = numActions;
            }

            public int GetNumberOfActions(TContext context)
            {
                return numActions;
            }
        }
    }
}
