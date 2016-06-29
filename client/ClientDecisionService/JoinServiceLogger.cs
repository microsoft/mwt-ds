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
        private readonly bool developmentMode;
        private IEventUploader interactionEventUploader;
        private IEventUploader observationEventUploader;

        internal JoinServiceLogger(string applicationID, bool developmentMode = false)
        {
            this.applicationID = applicationID;
            this.developmentMode = developmentMode;
            this.perfCounters = new PerformanceCounters(applicationID);
        }

        public void InitializeWithCustomAzureJoinServer(
            string loggingServiceBaseAddress,
            BatchingConfiguration batchConfig)
        {
            var eventUploader = new EventUploader(batchConfig, loggingServiceBaseAddress, developmentMode: this.developmentMode);
            eventUploader.InitializeWithToken(this.applicationID);

            this.interactionEventUploader = eventUploader;
        }

        public void InitializeWithAzureStreamAnalyticsJoinServer(
            string interactionEventHubConnectionString,
            string observationEventHubConnectionString,
            BatchingConfiguration interactionBatchConfig,
            BatchingConfiguration observationsBatchConfig)
        {
            this.interactionEventUploader = new EventUploaderASA(interactionEventHubConnectionString, interactionBatchConfig, developmentMode: this.developmentMode);
            this.interactionEventUploader.SuccessHandler += interactionBatchConfig_SuccessHandler;
            this.observationEventUploader = new EventUploaderASA(observationEventHubConnectionString, observationsBatchConfig, developmentMode: this.developmentMode);
            this.observationEventUploader.SuccessHandler += observationBatchConfig_SuccessHandler;
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

        public void Record(TContext context, TAction value, object explorerState, object mapperState, string uniqueKey)
        {
            this.interactionEventUploader.Upload(new Interaction
            {
                Key = uniqueKey,
                Context = context, 
                Value = value,
                ExplorerState = explorerState,
                MapperState = mapperState
            });
        }

        public void ReportReward(string uniqueKey, float reward)
        {
            (this.observationEventUploader ?? this.interactionEventUploader).Upload(new Observation
            {
                Key = uniqueKey,
                Value = reward
            });
        }

        public void ReportOutcome(string uniqueKey, object outcome)
        {
            (this.observationEventUploader ?? this.interactionEventUploader).Upload(new Observation
            {
                Key = uniqueKey,
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
