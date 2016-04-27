using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class JoinServiceLogger<TContext, TAction> : IRecorder<TContext, TAction>, ILogger, IDisposable
    {
        public void InitializeWithCustomAzureJoinServer(
            string authorizationToken,
            string loggingServiceBaseAddress,
            BatchingConfiguration batchConfig)
        {
            var eventUploader = new EventUploader(batchConfig, loggingServiceBaseAddress);
            eventUploader.InitializeWithToken(authorizationToken);

            this.eventUploader = eventUploader;
        }

        public void InitializeWithAzureStreamAnalyticsJoinServer(
            string eventHubConnectionString,
            string eventHubInputName,
            BatchingConfiguration batchConfig)
        {
            this.eventUploader = new EventUploaderASA(eventHubConnectionString, eventHubInputName, batchConfig);
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

        #region Members
        private IEventUploader eventUploader;
        #endregion
    }
}
