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
    /// Base interface for uploading data to the Join Server provided by Multiworld Testing Service.
    /// </summary>
    public interface IEventUploader : IDisposable
    {
        /// <summary>
        /// Sends a single event to the buffer for upload.
        /// </summary>
        /// <param name="e">The event to be uploaded.</param>
        void Upload(IEvent e);

        /// <summary>
        /// Sends a single event to the buffer for upload in a non-blocking fashion 
        /// where event is dropped if the buffer queue is full.
        /// </summary>
        /// <param name="e">The event to be uploaded.</param>
        /// <returns>true if the event was accepted into the buffer queue for processing.</returns>
        bool TryUpload(IEvent e);

        /// <summary>
        /// Sends multiple events to the buffer for upload.
        /// </summary>
        /// <param name="events">The list of events to be uploaded</param>
        void Upload(List<IEvent> events);

        /// <summary>
        /// Sends multiple events to the buffer for upload in a non-blocking fashion
        /// where events are dropped if the buffer queue is full.
        /// </summary>
        /// <param name="events">The list of events to be uploaded</param>
        /// <returns>true if all events were accepted into the buffer queue for processing.</returns>
        bool TryUpload(List<IEvent> events);

        /// <summary>
        /// Invoked if an error happened during the upload pipeline.
        /// </summary>
        event EventUploaderErrorEventHandler ErrorHandler;

        /// <summary>
        /// Invoked after the batch was successfully uploaded.
        /// </summary>
        event EventUploaderSuccessEventHandler SuccessHandler;

        event EventUploaderCompletedEventHandler CompletionHandler;
    }
}
