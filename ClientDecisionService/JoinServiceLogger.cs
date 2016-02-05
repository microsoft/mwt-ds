using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class JoinServiceLogger<TContext> : ILogger<TContext>, IDisposable
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

        public void Record(TContext context, uint[] actions, float probability, UniqueEventID uniqueKey, string modelId = null, bool? isExplore = null)
        {
            this.eventUploader.Upload(new MultiActionInteraction
            {
                Key = uniqueKey.Key,
                TimeStamp = uniqueKey.TimeStamp,
                Actions = actions,
                Probability = probability,
                Context = context,
                ModelId = modelId,
                IsExplore = isExplore
            });
        }

        public void Record(TContext context, uint action, float probability, UniqueEventID uniqueKey, string modelId = null, bool? isExplore = null)
        {
            this.eventUploader.Upload(new SingleActionInteraction
            {
                Key = uniqueKey.Key,
                TimeStamp = uniqueKey.TimeStamp,
                Action = action,
                Probability = probability,
                Context = context,
                ModelId = modelId,
                IsExplore = isExplore
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

        public void Flush()
        {
            this.eventUploader.Flush();
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
