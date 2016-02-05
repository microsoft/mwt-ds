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
    public class EventUploaderASA : BaseEventUploader<IEvent>
    {
        private string connectionString;
        private string eventHubInputName;
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
            this.client = EventHubClient.CreateFromConnectionString(builder.ToString(), this.eventHubInputName);
        }

        /// <summary>
        /// Transforms an event to another type suitable for uploading.
        /// </summary>
        /// <param name="sourceEvent">The source event to be transformed.</param>
        /// <returns>The transformed event to be uploaded.</returns>
        public override IEvent TransformEvent(IEvent sourceEvent)
        {
            return sourceEvent;
        }

        /// <summary>
        /// Measures the size of the transformed event in bytes.
        /// </summary>
        /// <param name="transformedEvent">The transformed event.</param>
        /// <returns>The size in bytes of the transformed event.</returns>
        public override int MeasureTransformedEvent(IEvent transformedEvent)
        {
            // TODO: BuildJsonMessage is called twice, during measure and during upload.
            return Encoding.UTF8.GetByteCount(BuildJsonMessage(transformedEvent));
        }

        /// <summary>
        /// Uploads multiple events to EventHub asynchronously.
        /// </summary>
        /// <param name="transformedEvents">The list of events to upload.</param>
        /// <returns>A Task object.</returns>
        public override async Task UploadTransformedEvents(IList<IEvent> transformedEvents)
        {
            await Task.WhenAll(transformedEvents.Select(e => this.UploadToEventHubAsync(e)));
        }

        /// <summary>
        /// Uploads a single event to EventHub asynchronously.
        /// </summary>
        /// <param name="events">The event to upload.</param>
        /// <returns>A Task object.</returns>
        private async Task UploadToEventHubAsync(IEvent events)
        {
            try
            {
                await this.client.SendAsync(BuildEventHubData(events));
            }
            catch (Exception exp)
            {
                Console.WriteLine("Error on send: " + exp.Message);
            }
        }

        /// <summary>
        /// Disposes all internal members.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                client.Close();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Converts an event to JSON format that is expected from EventHub.
        /// </summary>
        /// <param name="e">The event to convert.</param>
        /// <returns>Serialized JSON string.</returns>
        private static string BuildJsonMessage(IEvent e)
        {
            var jsonBuilder = new StringBuilder();
            jsonBuilder.Append("{\"EventId\":\"" + e.Key + "\",");
            jsonBuilder.Append("\"TimeStamp\":\"" + e.TimeStamp.ToString("o") + "\",");
            jsonBuilder.Append("\"j\":");
            jsonBuilder.Append(JsonConvert.SerializeObject(e));
            jsonBuilder.Append("}");
            return jsonBuilder.ToString();
        }

        /// <summary>
        /// Builds a data object to send to EventHub.
        /// </summary>
        /// <param name="e">The original event.</param>
        /// <returns>An EventData object.</returns>
        private static EventData BuildEventHubData(IEvent e)
        {
            var serializedString = BuildJsonMessage(e);
            return new EventData(Encoding.UTF8.GetBytes(serializedString))
            {
                PartitionKey = e.Key
            };
        }
    }
}
