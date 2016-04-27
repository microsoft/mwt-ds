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

namespace Microsoft.Research.MultiWorldTesting.JoinUploader
{
    /// <summary>
    /// Uploader class to interface with the Join Server provided by Multiworld Testing Service.
    /// </summary>
    /// <typeparam name="TTransformedEvent">The type of transformed event to be uploaded.</typeparam>
    public abstract class BaseEventUploader<TTransformedEvent> : IEventUploader
    {
        private readonly Random random;

        private TransformBlock<IEvent, TTransformedEvent> eventSource;
        private readonly IObserver<IEvent> eventObserver;
        private readonly ActionBlock<IList<TTransformedEvent>> eventProcessor;
        private IDisposable eventUnsubscriber;

        /// <summary>
        /// The batching configuration used when uploading data.
        /// </summary>
        protected readonly BatchingConfiguration batchConfig;

        /// <summary>
        /// Constructs an uploader object.
        /// </summary>
        /// <param name="batchConfig">Optional; The batching configuration that controls the buffer size.</param>
        public BaseEventUploader(BatchingConfiguration batchConfig = null)
        {
            this.batchConfig = batchConfig ?? new BatchingConfiguration();

            this.eventSource = new TransformBlock<IEvent, TTransformedEvent>(
                ev => TransformEventInternal(ev),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = this.batchConfig.MaxDegreeOfSerializationParallelism,
                    BoundedCapacity = this.batchConfig.MaxUploadQueueCapacity
                });
            this.eventObserver = this.eventSource.AsObserver();

            this.eventProcessor = new ActionBlock<IList<TTransformedEvent>>
            (
                (Func<IList<TTransformedEvent>, Task>)this.UploadTransformedEventsInternal, 
                new ExecutionDataflowBlockOptions 
                {
                    MaxDegreeOfParallelism = 16, // with the heavy number of cores out there, the memory can otherwise easily overflow
                    BoundedCapacity = this.batchConfig.MaxUploadQueueCapacity,
                }
            );

            this.eventUnsubscriber = this.eventSource.AsObservable()
                .Window(this.batchConfig.MaxDuration)
                .Select(w => w.Buffer(this.batchConfig.MaxEventCount, this.batchConfig.MaxBufferSizeInBytes, this.MeasureTransformedEventInternal))
                .SelectMany(buffer => buffer)
                .Subscribe(this.eventProcessor.AsObserver());

            this.random = new Random(0);
        }

        private TTransformedEvent TransformEventInternal(IEvent sourceEvent)
        {
            try
            {
                return this.TransformEvent(sourceEvent);
            }
            catch (Exception e)
            {
                this.batchConfig.FireErrorHandler(e);

                throw e;
            }
        }

        private int MeasureTransformedEventInternal(TTransformedEvent transformedEvent)
        {
            try
            {
                return this.MeasureTransformedEvent(transformedEvent);
            }
            catch (Exception e)
            {
                this.batchConfig.FireErrorHandler(e);

                throw e;
            }
        }

        private async Task UploadTransformedEventsInternal(IList<TTransformedEvent> transformedEvents)
        {
            try
            {
                await this.UploadTransformedEvents(transformedEvents);
                this.batchConfig.FireSuccessHandler(
                    transformedEvents.Count, 
                    transformedEvents.Sum(e => this.MeasureTransformedEvent(e)),
                    this.eventProcessor.InputCount);
            }
            catch (Exception e)
            {
                this.batchConfig.FireErrorHandler(e);

                throw e;
            }
        }

        /// <summary>
        /// Transforms an event to another type suitable for uploading.
        /// </summary>
        /// <param name="sourceEvent">The source event to be transformed.</param>
        /// <returns>The transformed event to be uploaded.</returns>
        public abstract TTransformedEvent TransformEvent(IEvent sourceEvent);

        /// <summary>
        /// Measures the size of the transformed event in bytes.
        /// </summary>
        /// <param name="transformedEvent">The transformed event.</param>
        /// <returns>The size in bytes of the transformed event.</returns>
        public abstract int MeasureTransformedEvent(TTransformedEvent transformedEvent);

        /// <summary>
        /// Triggered when a batch of events is ready for upload.
        /// </summary>
        /// <param name="transformedEvents">The list of JSON-serialized event strings.</param>
        /// <returns>Task</returns>
        public abstract Task UploadTransformedEvents(IList<TTransformedEvent> transformedEvents);

        /// <summary>
        /// Sends a single event to the buffer for upload.
        /// </summary>
        /// <param name="e">The event to be uploaded.</param>
        public void Upload(IEvent e) 
        {
            if (!this.DropEventRandomlyIfNeeded(e))
            {
                this.eventObserver.OnNext(e);
            }
        }

        /// <summary>
        /// Sends a single event to the buffer for upload in a non-blocking fashion 
        /// where event is dropped if the buffer queue is full.
        /// </summary>
        /// <param name="e">The event to be uploaded.</param>
        /// <returns>true if the event was accepted into the buffer queue for processing.</returns>
        public bool TryUpload(IEvent e)
        {
            if (!this.DropEventRandomlyIfNeeded(e))
            {
                return this.eventSource.Post(e);
            }
            return false;
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
        /// Sends multiple events to the buffer for upload in a non-blocking fashion
        /// where events are dropped if the buffer queue is full.
        /// </summary>
        /// <param name="events">The list of events to be uploaded</param>
        /// <returns>true if all events were accepted into the buffer queue for processing.</returns>
        public bool TryUpload(List<IEvent> events)
        {
            bool accepted = true;
            foreach (IEvent e in events)
            {
                accepted &= this.eventSource.Post(e);
            }
            return accepted;
        }

        /// <summary>
        /// Disposes the current object and all internal members.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes internal members.
        /// </summary>
        /// <param name="disposing">Whether the object is being disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.eventSource != null)
                {
                    // Flush the data buffer to upload all remaining events.
                    this.eventSource.Complete();
                    this.eventProcessor.Completion.Wait();

                    this.eventSource = null;
                }

                if (this.eventUnsubscriber != null)
                {
                    this.eventUnsubscriber.Dispose();
                    this.eventUnsubscriber = null;
                }
            }
        }

        /// <summary>
        /// Drop an interaction event if the input buffer queue is at certain capacity specified in the batch configuration. 
        /// In such cases, with probability Q, the event is dropped. Otherwise, it's modified so that its observed 
        /// probability P is set to P * (1 - Q). If the queue is below capacity, the event is left unchanged.
        /// </summary>
        /// <param name="e">The event.</param>
        /// <returns>true if the event is dropped.</returns>
        private bool DropEventRandomlyIfNeeded(IEvent e)
        {
            Interaction interaction = e as Interaction;
            if (interaction != null && this.eventSource.InputCount >= this.batchConfig.MaxUploadQueueCapacity * this.batchConfig.DroppingPolicy.MaxQueueLevelBeforeDrop)
            {
                // store probability of drop and actual prob will be computed server side
                if (this.batchConfig.DroppingPolicy.ProbabilityOfDrop > 0)
                    interaction.ProbabilityOfDrop = this.batchConfig.DroppingPolicy.ProbabilityOfDrop; 
                    
                if (this.random.NextDouble() <= this.batchConfig.DroppingPolicy.ProbabilityOfDrop)
                    return true;
            }
            return false;
        }
    }
}
