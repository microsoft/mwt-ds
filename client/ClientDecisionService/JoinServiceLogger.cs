using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using Microsoft.ApplicationInsights;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class JoinServiceLogger<TContext, TAction> : IRecorder<TContext, TAction>, ILogger, IDisposable
    {
        private readonly string applicationID;
        private readonly bool developmentMode;
        private IEventUploader interactionEventUploader;
        private IEventUploader observationEventUploader;
        private TelemetryClient telemetryClient;

        internal JoinServiceLogger(string applicationID, bool developmentMode = false)
        {
            this.applicationID = applicationID;
            this.developmentMode = developmentMode;

            this.telemetryClient = new TelemetryClient();
            this.telemetryClient.Context.User.Id = applicationID;
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
            this.interactionEventUploader.SuccessHandler += (source, eventCount, sumSize, inputQueueSize) => this.EventUploader_SuccessHandler(source, eventCount, sumSize, inputQueueSize, "Interaction");
            this.interactionEventUploader.ErrorHandler += EventUploader_ErrorHandler;
            this.interactionEventUploader.CompletionHandler += (source, blockName, task) => this.EventUploader_CompletionHandler(source, blockName, task, "Interaction");

            this.observationEventUploader = new EventUploaderASA(observationEventHubConnectionString, observationsBatchConfig, developmentMode: this.developmentMode);
            this.interactionEventUploader.SuccessHandler += (source, eventCount, sumSize, inputQueueSize) => this.EventUploader_SuccessHandler(source, eventCount, sumSize, inputQueueSize, "Observation");
            this.observationEventUploader.CompletionHandler += (source, blockName, task) => this.EventUploader_CompletionHandler(source, blockName, task, "Observation");
            this.observationEventUploader.ErrorHandler += EventUploader_ErrorHandler;
        }

        private void EventUploader_CompletionHandler(object source, string blockName, Task task, string name)
        {
            if (task.IsFaulted)
                this.telemetryClient.TrackException(
                    task.Exception, 
                    new Dictionary<string, string>
                    {
                        { "Event Uploader", name },
                        { "Block", blockName }
                    });
            else
                this.telemetryClient.TrackTrace($"Event Uploader '{name}/{blockName}' completed with status '{task.Status}'");
        }

        private void EventUploader_ErrorHandler(object source, Exception e)
        {
            this.telemetryClient.TrackException(e);
        }

        void EventUploader_SuccessHandler(object source, int eventCount, int sumSize, int inputQueueSize, string name)
        {
            this.telemetryClient.TrackMetric($"Client Library {name} Event Count", eventCount);
            this.telemetryClient.TrackMetric($"Client Library {name} Event Batch Size", sumSize);
            this.telemetryClient.TrackMetric($"Client Library {name} Queue", inputQueueSize);
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
