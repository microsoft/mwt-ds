using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class JoinServiceLogger<TContext, TAction> : IRecorder<TContext, TAction>, ILogger, IDisposable
    {
        private readonly PerformanceCounters perfCounters;
        private readonly string authorizationToken;
        private IEventUploader eventUploader;

        internal JoinServiceLogger(string authorizationToken)
        {
            this.authorizationToken = authorizationToken;
            this.perfCounters = new PerformanceCounters(authorizationToken);
        }

        public void InitializeWithCustomAzureJoinServer(
            string loggingServiceBaseAddress,
            BatchingConfiguration batchConfig)
        {
            var eventUploader = new EventUploader(batchConfig, loggingServiceBaseAddress);
            eventUploader.InitializeWithToken(this.authorizationToken);

            this.eventUploader = eventUploader;
        }

        public void InitializeWithAzureStreamAnalyticsJoinServer(
            string eventHubConnectionString,
            string eventHubInputName,
            BatchingConfiguration batchConfig)
        {
            batchConfig.SuccessHandler += batchConfig_SuccessHandler;

            this.eventUploader = new EventUploaderASA(eventHubConnectionString, eventHubInputName, batchConfig);
        }

        void batchConfig_SuccessHandler(object source, int eventCount, int sumSize, int inputQueueSize)
        {
            this.perfCounters.ReportExample(eventCount, sumSize);
            this.perfCounters.ReportExampleQueue(inputQueueSize);
        }

        public void Record(TContext context, TAction value, object explorerState, object mapperState, UniqueEventID uniqueKey)
        {
            this.eventUploader.Upload(new Interaction
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
            this.eventUploader.Upload(new Observation
            {
                Key = uniqueKey.Key,
                TimeStamp = uniqueKey.TimeStamp,
                Value = new { Reward = reward }
            });
        }

        public void ReportOutcome(UniqueEventID uniqueKey, object outcome)
        {
            this.eventUploader.Upload(new Observation
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
                if (this.eventUploader != null)
                {
                    this.eventUploader.Dispose();
                    this.eventUploader = null;
                }
            }
        }
    }
}
