using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Caching;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceClient<TContext, TValue, TExplorerState, TMapperValue> : IDisposable
    {
        private readonly TimeSpan settingsBlobPollDelay;

        private readonly string updateSettingsTaskId = "settings";

        private readonly IRecorder<TContext, TValue, TExplorerState> recorder;
        private readonly ILogger logger;

        protected readonly DecisionServiceConfiguration config;
        protected MwtExplorer<TContext, TValue, TExplorerState, TMapperValue> mwtExplorer;

        public DecisionServiceClient(
            DecisionServiceConfiguration config,
            ApplicationTransferMetadata metaData,
            IExplorer<TContext, TValue, TExplorerState, TMapperValue> explorer,
            IRecorder<TContext, TValue, TExplorerState> recorder = null)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (explorer == null)
                throw new ArgumentNullException("explorer");

            this.config = config;

            if (config.OfflineMode || metaData == null)
            {
                if (recorder == null)
                    throw new ArgumentException("A custom recorder must be defined when operating in offline mode.", "Recorder");

                this.recorder = recorder;
            }
            else
            {
                if (recorder != null)
                    this.recorder = recorder;
                else
                {
                    var joinServerLogger = new JoinServiceLogger();
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
                    // TODO: check if this cast actually works
                    this.recorder = (IRecorder<TContext, TValue, TExplorerState>)joinServerLogger;
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
            }

            this.logger = this.recorder as ILogger;
            this.mwtExplorer = MwtExplorer.Create(config.AuthorizationToken, this.recorder, explorer);
        }

        // TODO: rename?
        public TValue ChooseAction(UniqueEventID uniqueKey, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            return this.mwtExplorer.MapContext(uniqueKey, context, numActionsVariable);
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

    public static class DecisionServiceClient
    {
        private static MemoryCache dsCache = new MemoryCache("DecisionServiceCache");
        private static readonly TimeSpan DefaultExpirationTime = TimeSpan.FromHours(24);

        public static T AddOrGetExisting<T>(
            string token,
            Func<string, T> clientCreator, TimeSpan? expirationTime = null)
        {
            var obj = new Lazy<T>(() => clientCreator(token));

            var oldObj = (Lazy<T>)dsCache.AddOrGetExisting(
                token,
                obj,
                new CacheItemPolicy
                {
                    SlidingExpiration = expirationTime ?? DefaultExpirationTime,
                    RemovedCallback = (cacheEntryRemovedArguments) =>
                    {
                        var dsObject = cacheEntryRemovedArguments.CacheItem.Value as IDisposable;
                        if (dsObject != null)
                        {
                            dsObject.Dispose();
                        }
                    }
                });

            return (oldObj ?? obj).Value;
        }

        /// <summary>
        /// Remove and dispose all objects in the cache.
        /// </summary>
        public static void EvictAll()
        {
            var cacheKeyList = dsCache.Select(item => item.Key).ToList();
            foreach (var key in cacheKeyList)
                dsCache.Remove(key);
        }

        public static DecisionServiceClient<TContext, TValue, TExplorerState, TMapperValue>
            Create<TContext, TValue, TExplorerState, TMapperValue>(
                UnboundExplorer<TContext, TValue, TExplorerState, TMapperValue> explorer)
        {
            return new DecisionServiceClient<TContext, TValue, TExplorerState, TMapperValue>(
                explorer.ContextMapper.Configuration, explorer.ContextMapper.Metadata, explorer.Explorer);
        }
    }
}
