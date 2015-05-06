using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JoinServerUploader
{
    /// <summary>
    /// Uploader class to interface with the Join Server provided by Multiworld Testing Service.
    /// </summary>
    public class EventUploader : IDisposable
    {
        /// <summary>
        /// Constructs an uploader object.
        /// </summary>
        public EventUploader() : this(null, null) { }

        /// <summary>
        /// Constructs an uploader object.
        /// </summary>
        /// <param name="batchConfig">The batching configuration that controls the buffer size.</param>
        /// <param name="loggingServiceBaseAddress">The address of a custom HTTP logging service. When null, the join service address is used.</param>
        public EventUploader(BatchingConfiguration batchConfig, string loggingServiceBaseAddress)
        {
            this.batchConfig = batchConfig ?? new BatchingConfiguration()
            {
                MaxBufferSizeInBytes = 4 * 1024 * 1024,
                MaxDuration = TimeSpan.FromMinutes(1),
                MaxEventCount = 10000,
                MaxUploadQueueCapacity = 100,
                UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
            };

            this.loggingServiceBaseAddress = loggingServiceBaseAddress ?? Constants.ServiceAddress;

            this.eventSource = new TransformBlock<IEvent, string>(ev => JsonConvert.SerializeObject(new ExperimentalUnitFragment { Key = ev.Key, Value = ev }),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    BoundedCapacity = this.batchConfig.MaxUploadQueueCapacity
                });
            this.eventObserver = this.eventSource.AsObserver();

            this.eventProcessor = new ActionBlock<IList<string>>((Func<IList<string>, Task>)this.BatchProcess, new ExecutionDataflowBlockOptions
            {
                // TODO: Finetune these numbers
                MaxDegreeOfParallelism = Environment.ProcessorCount * 4,
                BoundedCapacity = this.batchConfig.MaxUploadQueueCapacity,
            });

            this.eventUnsubscriber = this.eventSource.AsObservable()
                .Window(this.batchConfig.MaxDuration)
                .Select(w => w.Buffer(this.batchConfig.MaxEventCount, this.batchConfig.MaxBufferSizeInBytes, json => Encoding.UTF8.GetByteCount(json)))
                .SelectMany(buffer => buffer)
                .Subscribe(this.eventProcessor.AsObserver());
        }

        /// <summary>
        /// Initialize the uploader to perform requests using the specified authorization token.
        /// </summary>
        /// <param name="authorizationToken">The token that is used for resource access authentication by the join service.</param>
        public void InitializeWithToken(string authorizationToken)
        {
            Initialize(Constants.TokenAuthenticationScheme, authorizationToken);
        }

        /// <summary>
        /// Initialize the uploader to perform requests using the specified connection string.
        /// </summary>
        /// <param name="connectionString">The connection string that is used to access resources by the join service.</param>
        /// <param name="experimentalUnitDuration">The duration of the experimental unit during which events are joined by the join service.</param>
        public void InitializeWithConnectionString(string connectionString, int experimentalUnitDuration)
        {
            if (experimentalUnitDuration <= 0)
            {
                throw new ArgumentException("Experimental Unit Duration must be a valid positive number", "experimentalUnitDuration");
            }

            Initialize(Constants.ConnectionStringAuthenticationScheme, connectionString);
            this.experimentalUnitDuration = experimentalUnitDuration;
        }

        /// <summary>
        /// Sends a single event to the buffer for upload.
        /// </summary>
        /// <param name="e">The event to be uploaded.</param>
        public void Upload(IEvent e) 
        {
            this.eventObserver.OnNext(e);
        }

        /// <summary>
        /// Sends multiple events to the buffer for upload.
        /// </summary>
        /// <param name="events">The list of events to be uploaded</param>
        public void Upload(List<IEvent> events)
        {
            foreach (IEvent e in events)
            {
                this.eventObserver.OnNext(e);
            }
        }

        /// <summary>
        /// Initialize the HTTP client with proper authentication scheme.
        /// </summary>
        /// <param name="authenticationScheme">The authentication scheme.</param>
        /// <param name="authenticationValue">The authentication value.</param>
        private void Initialize(string authenticationScheme, string authenticationValue)
        {
            if (this.httpClient != null)
            {
                throw new InvalidOperationException("Uploader can only be initialized once.");
            }
            this.httpClient = new HttpClient();
            this.httpClient.BaseAddress = new Uri(this.loggingServiceBaseAddress);
            this.httpClient.Timeout = Constants.ConnectionTimeOut;
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(authenticationScheme, authenticationValue);
        }

        /// <summary>
        /// Triggered when a batch of events is ready for upload.
        /// </summary>
        /// <param name="jsonExpFragments">The list of JSON-serialized event strings.</param>
        /// <returns>Task</returns>
        private async Task BatchProcess(IList<string> jsonExpFragments)
        {
            EventBatch batch = new EventBatch
            {
                Id = Guid.NewGuid(),
                JsonEvents = jsonExpFragments
            };

            byte[] jsonByteArray = Encoding.UTF8.GetBytes(EventUploader.BuildJsonMessage(batch, this.experimentalUnitDuration));

            using (var jsonMemStream = new MemoryStream(jsonByteArray))
            {
                HttpResponseMessage response = null;

                if (batchConfig.UploadRetryPolicy == BatchUploadRetryPolicy.ExponentialRetry)
                {
                    var retryStrategy = new ExponentialBackoff(Constants.RetryCount,
                    Constants.RetryMinBackoff, Constants.RetryMaxBackoff, Constants.RetryDeltaBackoff);

                    RetryPolicy retryPolicy = new RetryPolicy<JoinServiceTransientErrorDetectionStrategy>(retryStrategy);

                    try
                    {
                        response = await retryPolicy.ExecuteAsync(async () =>
                        {
                            HttpResponseMessage currentResponse = null;
                            try
                            {
                                currentResponse = await httpClient.PostAsync(Constants.ServicePostAddress, new StreamContent(jsonMemStream)).ConfigureAwait(false);
                            }
                            catch (TaskCanceledException e) // HttpClient throws this on timeout
                            {
                                // Convert to a different exception otherwise ExecuteAsync will see cancellation
                                throw new HttpRequestException("Request timed out", e);
                            }
                            return currentResponse.EnsureSuccessStatusCode();
                        });
                    }
                    catch (Exception ex)
                    {
                        this.RaiseSendFailedEvent(batch, ex);
                        return;
                    }
                }
                else
                {
                    response = await httpClient.PostAsync(Constants.ServicePostAddress, new StreamContent(jsonMemStream)).ConfigureAwait(false);
                }

                if (response == null)
                {
                    this.RaiseSendFailedEvent(batch, new HttpRequestException("No response received from the server."));
                }
                else if (!response.IsSuccessStatusCode)
                {
                    string reason = await response.Content.ReadAsStringAsync();
                    this.RaiseSendFailedEvent(batch, new HttpRequestException(reason));
                }
                else
                {
                    this.RaiseSentEvent(batch);
                }
            }
        }

        /// <summary>
        /// Flush the data buffer to upload all remaining events.
        /// </summary>
        public void Flush()
        {
            this.eventSource.Complete();
            this.eventProcessor.Completion.Wait();
        }

        /// <summary>
        /// Disposes the current object and all internal members.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
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

        private static string BuildJsonMessage(EventBatch batch, int? experimentalUnitDuration)
        {
            StringBuilder jsonBuilder = new StringBuilder();

            jsonBuilder.Append("{\"i\":\"" + batch.Id.ToString() + "\",");

            jsonBuilder.Append("\"j\":[");
            jsonBuilder.Append(String.Join(",", batch.JsonEvents));
            jsonBuilder.Append("]");

            if (experimentalUnitDuration.HasValue)
            {
                jsonBuilder.Append(",\"d\":");
                jsonBuilder.Append(experimentalUnitDuration.Value);
            }

            jsonBuilder.Append("}");

            return jsonBuilder.ToString();
        }

        private void RaiseSentEvent(EventBatch batch)
        {
            if (batch != null)
            {
                if (batch.JsonEvents != null)
                {
                    Trace.TraceInformation("Successfully uploaded batch with {0} events.", batch.JsonEvents.Count);
                }
                if (PackageSent != null)
                {
                    PackageSent(this, new PackageEventArgs { PackageId = batch.Id, Records = batch.JsonEvents });
                }
            }
        }

        private void RaiseSendFailedEvent(EventBatch batch, Exception ex)
        {
            if (batch != null)
            {
                if (ex != null)
                {
                    Trace.TraceError("Unable to upload batch: " + ex.ToString());
                }
                if (PackageSendFailed != null)
                {
                    PackageSendFailed(this, new PackageEventArgs { PackageId = batch.Id, Records = batch.JsonEvents, Exception = ex });
                }
            }
        }

        /// <summary>
        /// Occurs when a package was successfully uploaded to the join server.
        /// </summary>
        public event PackageSentEventHandler PackageSent;

        /// <summary>
        /// Occurs when a package was not successfully uploaded to the join server.
        /// </summary>
        public event PackageSendFailedEventHandler PackageSendFailed;

        private readonly BatchingConfiguration batchConfig;
        private readonly string loggingServiceBaseAddress;
        private int? experimentalUnitDuration;

        private readonly TransformBlock<IEvent, string> eventSource;
        private readonly IObserver<IEvent> eventObserver;
        private readonly ActionBlock<IList<string>> eventProcessor;
        private IDisposable eventUnsubscriber;

        private HttpClient httpClient;
    }
}
