using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using Microsoft.Research.MultiWorldTesting.Contract;
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

namespace Microsoft.Research.DecisionService.Uploader
{
    /// <summary>
    /// Uploader class to interface with the Join Server provided by Multiworld Testing Service.
    /// </summary>
    public class EventUploader : BaseEventUploader<string>
    {
        private readonly string loggingServiceBaseAddress;
        private int? experimentalUnitDuration;
        private Func<IEvent, string> eventSerializer;

        private IHttpClient httpClient;

        /// <summary>
        /// Occurs when a package was successfully uploaded to the join server.
        /// </summary>
        public event PackageSentEventHandler PackageSent;

        /// <summary>
        /// Occurs when a package was not successfully uploaded to the join server.
        /// </summary>
        public event PackageSendFailedEventHandler PackageSendFailed;

        /// <summary>
        /// Constructs an uploader object.
        /// </summary>
        public EventUploader() : this(null, null, null) { }

        /// <summary>
        /// Constructs an uploader object.
        /// </summary>
        /// <param name="batchConfig">Optional; The batching configuration that controls the buffer size.</param>
        /// <param name="loggingServiceBaseAddress">Optional; The address of a custom HTTP logging service. When null, the join service address is used.</param>
        /// <param name="httpClient">Optional; The custom <see cref="IHttpClient"/> object to handle HTTP requests.</param>
        public EventUploader(BatchingConfiguration batchConfig = null, string loggingServiceBaseAddress = null, IHttpClient httpClient = null) : base(batchConfig)
        {
            this.loggingServiceBaseAddress = loggingServiceBaseAddress ?? ServiceConstants.JoinAddress;

            this.httpClient = httpClient ?? new UploaderHttpClient();
            
            // override ReferenceResolver is supplied
            if (this.batchConfig.ReferenceResolver == null)
            {
                this.eventSerializer = ev => JsonConvert.SerializeObject(new ExperimentalUnitFragment { Key = ev.Key, Value = ev });
            }
            else
            {
                this.eventSerializer = ev => JsonConvert.SerializeObject(
                    new ExperimentalUnitFragment { Key = ev.Key, Value = ev },
                    Formatting.None,
                    new JsonSerializerSettings { ReferenceResolver = this.batchConfig.ReferenceResolver });
            }
        }

        /// <summary>
        /// Initialize the uploader to perform requests using the specified authorization token.
        /// </summary>
        /// <param name="authorizationToken">The token that is used for resource access authentication by the join service.</param>
        public void InitializeWithToken(string authorizationToken)
        {
            Initialize(ServiceConstants.TokenAuthenticationScheme, authorizationToken);
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

            Initialize(ServiceConstants.ConnectionStringAuthenticationScheme, connectionString);
            this.experimentalUnitDuration = experimentalUnitDuration;
        }

        /// <summary>
        /// Initialize the HTTP client with proper authentication scheme.
        /// </summary>
        /// <param name="authenticationScheme">The authentication scheme.</param>
        /// <param name="authenticationValue">The authentication value.</param>
        private void Initialize(string authenticationScheme, string authenticationValue)
        {
            this.httpClient.Initialize(this.loggingServiceBaseAddress, Constants.ConnectionTimeOut, authenticationScheme, authenticationValue);
        }

        /// <summary>
        /// Transforms an event to a string suitable for uploading.
        /// </summary>
        /// <param name="sourceEvent">The source event to be transformed.</param>
        /// <returns>The transformed string to be uploaded.</returns>
        public override string TransformEvent(IEvent sourceEvent)
        {
            return this.eventSerializer(sourceEvent);
        }

        /// <summary>
        /// Measures the size of the transformed event in bytes.
        /// </summary>
        /// <param name="transformedEvent">The transformed event.</param>
        /// <returns>The size in bytes of the transformed event.</returns>
        public override int MeasureTransformedEvent(string transformedEvent)
        {
            return Encoding.UTF8.GetByteCount(transformedEvent);
        }

        /// <summary>
        /// Triggered when a batch of events is ready for upload.
        /// </summary>
        /// <param name="transformedEvents">The list of JSON-serialized event strings.</param>
        /// <returns>Task</returns>
        public override async Task UploadTransformedEvents(IList<string> transformedEvents)
        {
            EventBatch batch = new EventBatch
            {
                Id = Guid.NewGuid(),
                JsonEvents = transformedEvents
            };

            string json = EventUploader.BuildJsonMessage(batch, this.experimentalUnitDuration);

            IHttpResponse response = null;

            if (batchConfig.UploadRetryPolicy == BatchUploadRetryPolicy.ExponentialRetry)
            {
                var retryStrategy = new ExponentialBackoff(Constants.RetryCount,
                Constants.RetryMinBackoff, Constants.RetryMaxBackoff, Constants.RetryDeltaBackoff);

                RetryPolicy retryPolicy = new RetryPolicy<JoinServiceTransientErrorDetectionStrategy>(retryStrategy);

                try
                {
                    response = await retryPolicy.ExecuteAsync(async () =>
                    {
                        IHttpResponse currentResponse = null;
                        try
                        {
                            currentResponse = await httpClient.PostAsync(ServiceConstants.JoinPostAddress, json);
                        }
                        catch (TaskCanceledException e) // HttpClient throws this on timeout
                        {
                            // Convert to a different exception otherwise ExecuteAsync will see cancellation
                            throw new HttpRequestException("Request timed out", e);
                        }
                        return currentResponse;
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
                response = await httpClient.PostAsync(ServiceConstants.JoinPostAddress, json);
            }

            if (response == null)
            {
                this.RaiseSendFailedEvent(batch, new HttpRequestException("No response received from the server."));
            }
            else if (!response.IsSuccessStatusCode)
            {
                string reason = await response.GetContentAsync();
                this.RaiseSendFailedEvent(batch, new HttpRequestException(reason));
            }
            else
            {
                this.RaiseSentEvent(batch);
            }
        }

        /// <summary>
        /// Disposes all internal members.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.httpClient != null)
                {
                    this.httpClient.Dispose();
                    this.httpClient = null;
                }
            }
            base.Dispose(disposing);
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
    }
}
