using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
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
using System.Collections.Concurrent;

namespace Microsoft.Research.MultiWorldTesting.JoinUploader
{
    /// <summary>
    /// Uploader class to interface with the ASA-based Join Server provided by user applications.
    /// </summary>
    public class EventUploaderASA : BaseEventUploader<EventData>
    {
        private readonly string connectionString;
        private readonly string eventHubInputName;
        private EventHubClient client;

        /// <summary>
        /// Constructs an uploader object.
        /// </summary>
        /// <param name="eventHubConnectionString">The Azure Stream Analytics connection string.</param>
        /// <param name="eventHubInputName">The EventHub input name where data are sent to.</param>
        /// <param name="batchConfig">Optional; The batching configuration to used when uploading data.</param>
        public EventUploaderASA
        (
            string eventHubConnectionString, 
            string eventHubInputName, 
            BatchingConfiguration batchConfig = null
        ) 
        : base(batchConfig)
        {
            this.connectionString = eventHubConnectionString;
            this.eventHubInputName = eventHubInputName;
            
            var builder = new ServiceBusConnectionStringBuilder(this.connectionString)
            {
                TransportType = TransportType.Amqp
            };
            
            if (this.batchConfig.ReUseTcpConnection)
            {
                this.client = EventHubClient.CreateFromConnectionString(builder.ToString(), this.eventHubInputName);
            }
            else
            {
                var factory = MessagingFactory.CreateFromConnectionString(builder.ToString());
                this.client = factory.CreateEventHubClient(this.eventHubInputName);
            }
        }

        /// <summary>
        /// Transforms an event to another type suitable for uploading.
        /// </summary>
        /// <param name="sourceEvent">The source event to be transformed.</param>
        /// <returns>The transformed event to be uploaded.</returns>
        public override EventData TransformEvent(IEvent sourceEvent)
        {
            var json = JsonConvert.SerializeObject(sourceEvent);
            var bytes = Encoding.UTF8.GetBytes(json);

            var partitionKey = this.batchConfig.PartitionCount == null ?
                sourceEvent.Key : (sourceEvent.Key.GetHashCode() % this.batchConfig.PartitionCount).ToString();

            return new EventData(bytes) { PartitionKey = partitionKey };
        }

        /// <summary>
        /// Measures the size of the transformed event in bytes.
        /// </summary>
        /// <param name="transformedEvent">The transformed event.</param>
        /// <returns>The size in bytes of the transformed event.</returns>
        public override int MeasureTransformedEvent(EventData transformedEvent)
        {
            return (int)transformedEvent.SerializedSizeInBytes;
        }

        /// <summary>
        /// Uploads multiple events to EventHub asynchronously.
        /// </summary>
        /// <param name="transformedEvents">The list of events to upload.</param>
        /// <returns>A Task object.</returns>
        public override async Task UploadTransformedEvents(IList<EventData> transformedEvents)
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // group equal partition keys
                    var tasks = transformedEvents
                        .GroupBy(e => e.PartitionKey)
                        .Select(evt => this.client.SendBatchAsync(evt));

                    await Task.WhenAll(tasks);
                    return;
                }
                catch (ServerBusyException e)
                {
                    if (!e.IsTransient)
                    {
                        this.batchConfig.FireErrorHandler(e);
                        return;
                    }
                }
                catch (Exception e)
                {
                    this.batchConfig.FireErrorHandler(e);
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(4));
            }
        }

        /// <summary>
        /// Uploads a single event to EventHub asynchronously.
        /// </summary>
        /// <param name="events">The event to upload.</param>
        /// <returns>A Task object.</returns>
        private Task UploadToEventHubAsync(EventData evt)
        {
            return this.client.SendAsync(evt);
        }

        /// <summary>
        /// Disposes all internal members.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (this.client != null)
                {
                    this.client.Close();
                    this.client = null;
                }
            }
        }
    }
}
