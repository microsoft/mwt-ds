using MultiWorldTesting;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Diagnostics;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace ClientDecisionService
{
    internal class DecisionServiceRecorder<TContext> : IRecorder<TContext>, IDisposable
    {
        public DecisionServiceRecorder(BatchingConfiguration batchConfig, 
            Func<TContext, string> contextSerializer, 
            string authorizationToken) 
        {
            this.batchConfig = batchConfig;
            this.contextSerializer = contextSerializer;

            this.httpClient = new HttpClient();
            this.httpClient.BaseAddress = new Uri(DecisionServiceConstants.ServiceAddress);
            this.httpClient.Timeout = DecisionServiceConstants.ConnectionTimeOut;
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(DecisionServiceConstants.AuthenticationScheme, authorizationToken);

            // TODO: Switch to using latency-link upload strategy?
            this.eventSource = new TransformBlock<IEvent, string>(ev => JsonConvert.SerializeObject(new ExperimentalUnitFragment { Id = ev.ID, Value = ev }), 
                new ExecutionDataflowBlockOptions
            { 
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                BoundedCapacity = batchConfig.MaxUploadQueueCapacity
            });
            this.eventObserver = this.eventSource.AsObserver();

            this.eventProcessor = new ActionBlock<IList<string>>((Func<IList<string>, Task>)this.BatchProcess, new ExecutionDataflowBlockOptions 
            { 
                // TODO: Finetune these numbers
                MaxDegreeOfParallelism = Environment.ProcessorCount * 4,
                BoundedCapacity = batchConfig.MaxUploadQueueCapacity,
            });

            this.eventUnsubscriber = this.eventSource.AsObservable()
                .Window(batchConfig.MaxDuration)
                .Select(w => w.Buffer(batchConfig.MaxEventCount, batchConfig.MaxBufferSizeInBytes, json => Encoding.UTF8.GetByteCount(json)))
                .SelectMany(buffer => buffer)
                .Subscribe(this.eventProcessor.AsObserver());
        }

        // TODO: add a TryRecord that doesn't block and returns whether the operation was successful
        // TODO: alternatively we could also use a Configuration setting to control how Record() behaves
        public void Record(TContext context, uint action, float probability, string uniqueKey)
        {
            // Blocking call if queue is full.
            this.eventObserver.OnNext(new Interaction
            { 
                ID = uniqueKey,
                Action = (int)action,
                Probability = probability,
                Context = this.contextSerializer(context)
            });
        }

        public void ReportReward(float reward, string uniqueKey)
        {
            this.eventObserver.OnNext(new Observation
            {
                ID = uniqueKey,
                Value = JsonConvert.SerializeObject(new { Reward = reward })
            });
        }

        public bool TryReportReward(float reward, string uniqueKey)
        {
            return this.eventSource.Post(new Observation
            {
                ID = uniqueKey,
                Value = JsonConvert.SerializeObject(reward)
            });
        }

        public void ReportOutcome(string outcomeJson, string uniqueKey)
        {
            this.eventObserver.OnNext(new Observation
            { 
                ID = uniqueKey,
                Value = outcomeJson
            });
        }

        public bool TryReportOutcome(string outcomeJson, string uniqueKey)
        {
            return this.eventSource.Post(new Observation
            {
                ID = uniqueKey,
                Value = outcomeJson
            });
        }

        private async Task BatchProcess(IList<string> jsonExpFragments)
        {
            EventBatch batch = new EventBatch { 
                ID = Guid.NewGuid(),
                JsonEvents = jsonExpFragments
            };

            byte[] jsonByteArray = Encoding.UTF8.GetBytes(this.BuildJsonMessage(batch));

            using (var jsonMemStream = new MemoryStream(jsonByteArray))
            {
                HttpResponseMessage response = null;

                if (batchConfig.UploadRetryPolicy == BatchUploadRetryPolicy.Retry)
                {
                    var retryStrategy = new ExponentialBackoff(DecisionServiceConstants.RetryCount,
                    DecisionServiceConstants.RetryMinBackoff, DecisionServiceConstants.RetryMaxBackoff, DecisionServiceConstants.RetryDeltaBackoff);

                    RetryPolicy retryPolicy = new RetryPolicy<DecisionServiceTransientErrorDetectionStrategy>(retryStrategy);

                    response = await retryPolicy.ExecuteAsync(async () =>
                    {
                        HttpResponseMessage currentResponse = null;
                        try
                        {
                            currentResponse = await httpClient.PostAsync(DecisionServiceConstants.ServicePostAddress, new StreamContent(jsonMemStream)).ConfigureAwait(false);
                        }
                        catch (TaskCanceledException e) // HttpClient throws this on timeout
                        {
                            // Convert to a different exception otherwise ExecuteAsync will see cancellation
                            throw new HttpRequestException("Request timed out", e);
                        }
                        return currentResponse.EnsureSuccessStatusCode();
                    });
                }
                else
                {
                    response = await httpClient.PostAsync(DecisionServiceConstants.ServicePostAddress, new StreamContent(jsonMemStream)).ConfigureAwait(false);
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    Trace.TraceError("Unable to upload batch: " + await response.Content.ReadAsStringAsync());
                }
                else
                {
                    Trace.TraceInformation("Successfully uploaded batch with {0} events.", batch.JsonEvents.Count);
                }
            }
        }

        public void Flush()
        { 
            this.eventSource.Complete();
            this.eventProcessor.Completion.Wait();
        }

        private string BuildJsonMessage(EventBatch batch)
        {
            StringBuilder jsonBuilder = new StringBuilder();

            jsonBuilder.Append("{\"i\":\"" + batch.ID.ToString() + "\",");
            
            jsonBuilder.Append("\"j\":[");
            jsonBuilder.Append(String.Join(",", batch.JsonEvents));
            jsonBuilder.Append("]}");

            return jsonBuilder.ToString();
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
                // free managed resources
                if (this.httpClient != null)
                {
                    this.httpClient.Dispose();
                    this.httpClient = null;
                }

                if (this.eventUnsubscriber != null)
                {
                    this.eventUnsubscriber.Dispose();
                    this.eventUnsubscriber = null;
                }
            }
        }

        #region Members
        private readonly BatchingConfiguration batchConfig;
        private readonly Func<TContext, string> contextSerializer;
        private readonly TransformBlock<IEvent, string> eventSource;
        private readonly IObserver<IEvent> eventObserver;
        private readonly ActionBlock<IList<string>> eventProcessor;
        private IDisposable eventUnsubscriber;
        private HttpClient httpClient;
        #endregion
    }
}
