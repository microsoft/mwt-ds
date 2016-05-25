using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class JoinServiceLogger<TContext, TAction> : IRecorder<TContext, TAction>, ILogger, IDisposable
    {
        private readonly PerformanceCounters perfCounters;
        private readonly string applicationID;
        private IEventUploader interactionEventUploader;
        private IEventUploader observationEventUploader;

        internal JoinServiceLogger(string applicationID)
        {
            this.applicationID = applicationID;
            this.perfCounters = new PerformanceCounters(applicationID);
        }

        public void InitializeWithCustomAzureJoinServer(
            string loggingServiceBaseAddress,
            BatchingConfiguration batchConfig)
        {
            var eventUploader = new EventUploader(batchConfig, loggingServiceBaseAddress);
            eventUploader.InitializeWithToken(this.applicationID);

            this.interactionEventUploader = eventUploader;
        }

        public void InitializeWithAzureStreamAnalyticsJoinServer(
            string interactionEventHubConnectionString,
            string observationEventHubConnectionString,
            BatchingConfiguration interactionBatchConfig,
            BatchingConfiguration observationsBatchConfig)
        {
            interactionBatchConfig.SuccessHandler += interactionBatchConfig_SuccessHandler;
            observationsBatchConfig.SuccessHandler += observationBatchConfig_SuccessHandler;

            this.interactionEventUploader = new EventUploaderASA(interactionEventHubConnectionString, interactionBatchConfig);
            this.observationEventUploader = new EventUploaderASA(observationEventHubConnectionString, observationsBatchConfig);
        }

        void interactionBatchConfig_SuccessHandler(object source, int eventCount, int sumSize, int inputQueueSize)
        {
            this.perfCounters.ReportInteraction(eventCount, sumSize);
            this.perfCounters.ReportInteractionExampleQueue(inputQueueSize);
        }

        void observationBatchConfig_SuccessHandler(object source, int eventCount, int sumSize, int inputQueueSize)
        {
            this.perfCounters.ReportObservation(eventCount, sumSize);
            this.perfCounters.ReportObservationExampleQueue(inputQueueSize);
        }

        public void Record(TContext context, TAction value, object explorerState, object mapperState, UniqueEventID uniqueKey)
        {
            this.interactionEventUploader.Upload(new Interaction
            {
                Key = uniqueKey.Key,
                TimeStamp = uniqueKey.TimeStamp,
                Context = context, 
                Value = value,
                ExplorerState = explorerState,
                MapperState = mapperState
            });
        }

        public void ReportReward(UniqueEventID uniqueKey, float reward)
        {
            (this.observationEventUploader ?? this.interactionEventUploader).Upload(new Observation
            {
                Key = uniqueKey.Key,
                TimeStamp = uniqueKey.TimeStamp,
                Value = new { Reward = reward }
            });
        }

        public void ReportOutcome(UniqueEventID uniqueKey, object outcome)
        {
            (this.observationEventUploader ?? this.interactionEventUploader).Upload(new Observation
            {
                Key = uniqueKey.Key,
                TimeStamp = uniqueKey.TimeStamp,
                Value = outcome
            });
        }
        
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.interactionEventUploader != null)
                {
                    this.interactionEventUploader.Dispose();
                    this.interactionEventUploader = null;
                }

                if (this.observationEventUploader != null)
                {
                    this.observationEventUploader.Dispose();
                    this.observationEventUploader = null;
                }
            }
        }
    }
}
