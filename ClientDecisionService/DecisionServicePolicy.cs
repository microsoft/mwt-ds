using MultiWorldTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;

namespace ClientDecisionService
{
    internal class DecisionServicePolicy<TContext> : IPolicy<TContext>, IDisposable
    {
        public DecisionServicePolicy(Action notifyPolicyUpdate, string modelAddress, string modelOutputDir)
        {
            this.notifyPolicyUpdate = notifyPolicyUpdate;
            this.modelAddress = modelAddress;
            this.modelOutputDir = string.IsNullOrWhiteSpace(modelOutputDir) ? string.Empty : modelOutputDir;

            this.cancellationToken = new CancellationTokenSource();
            this.pollFinishedEvent = new ManualResetEventSlim();

            this.worker = new BackgroundWorker();
            this.worker.WorkerReportsProgress = true;
            this.worker.DoWork += PollForUpdate;
            this.worker.ProgressChanged += FoundUpdate;
            this.worker.RunWorkerAsync(this.cancellationToken);
        }

        public uint ChooseAction(TContext context)
        {
            string exampleLine = string.Format(CultureInfo.InvariantCulture, "1: | {0}", context);

            uint action = 0;

            lock (this.vwLock)
            {
                IntPtr example = VowpalWabbitInterface.ReadExample(vw, exampleLine);
                VowpalWabbitInterface.Predict(vw, example);
                VowpalWabbitInterface.FinishExample(vw, example);
                action = (uint)VowpalWabbitInterface.GetCostSensitivePrediction(example);
            }

            return action;
        }

        public void StopPolling()
        {
            this.cancellationToken.Cancel();

            if (!this.pollFinishedEvent.Wait(DecisionServiceConstants.PollCancelWait))
            {
                Trace.TraceWarning("Timed out waiting for model polling task: {0} ms.", DecisionServiceConstants.PollCancelWait);
            }

            lock (vwLock)
            {
                this.VWFinish();
            }
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
                if (this.worker != null)
                {
                    this.worker.Dispose();
                    this.worker = null;
                }
            }

            lock (vwLock)
            {
                this.VWFinish();
            }
        }

        void FoundUpdate(object sender, ProgressChangedEventArgs e)
        {
            var newModelFileName = e.UserState as string;

            bool modelUpdateSuccess = true;
            lock (vwLock)
            {
                try
                {
                    this.VWFinish(); // Finish previous run before initializing on new file
                    this.VWInitialize(string.Format(CultureInfo.InvariantCulture, "-t -i {0}", Path.Combine(this.modelOutputDir, newModelFileName)));
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Unable to initialize VW.");
                    Trace.TraceError(ex.ToString());
                    modelUpdateSuccess = false;
                }
            }

            if (modelUpdateSuccess)
            {
                this.notifyPolicyUpdate();
            }
            else
            {
                Trace.TraceInformation("Attempt to update model failed.");
            }
        }

        void PollForUpdate(object sender, DoWorkEventArgs e)
        {
            var cancelToken = e.Argument as CancellationTokenSource;

            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    bool cancelled = cancelToken.Token.WaitHandle.WaitOne(DecisionServiceConstants.PollDelay);
                    if (cancelled)
                    {
                        Trace.TraceInformation("Cancellation request received while sleeping.");
                        break;
                    }

                    try
                    {
                        HttpWebRequest request = (HttpWebRequest) WebRequest.Create(modelAddress);
                        if (modelDate != null)
                        {
                            request.IfModifiedSince = modelDate.UtcDateTime;
                        }

                        using (WebResponse response = request.GetResponse())
                        using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                        {
                            var model = JsonConvert.DeserializeObject<ModelTransferData>(sr.ReadToEnd());

                            // Write model to file
                            if (!string.IsNullOrWhiteSpace(this.modelOutputDir))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(this.modelOutputDir));
                            }

                            File.WriteAllBytes(Path.Combine(this.modelOutputDir, model.Name), Convert.FromBase64String(model.ContentAsBase64));

                            // Store last modified date for conditional get
                            modelDate = model.LastModified;

                            Trace.TraceInformation("Retrieved new model: {0}", model.Name);

                            // Notify caller of model update
                            worker.ReportProgress(0, model.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMessages = new List<string>(new string[]
                        {
                            "Failed to retrieve new model information.",
                            ex.ToString()
                        });

                        bool logErrors = true;
                        if (ex is WebException)
                        {
                            HttpWebResponse httpResponse = ((WebException) ex).Response as HttpWebResponse;

                            switch (httpResponse.StatusCode)
                            {
                                case HttpStatusCode.NotModified:
                                    // Exception is raised for NotModified http response but this is expected.
                                    logErrors = false;
                                    break;
                                default:
                                    errorMessages.Add(httpResponse.StatusDescription);
                                    break;
                            }
                        }
                        else if (ex is UnauthorizedAccessException)
                        {
                            Trace.TraceError("Unable to write model to disk due to restricted access. Model polling will stop.");
                            cancelToken.Cancel();
                        }
                        if (logErrors)
                        {
                            foreach (string message in errorMessages)
                            {
                                Trace.TraceError(message);
                            }
                        }
                    }
                }
            }
            finally
            {
                this.pollFinishedEvent.Set();
                Trace.TraceInformation("Model polling has been cancelled per request.");
            }
        }

        void VWInitialize(string arguments)
        {
            vw = VowpalWabbitInterface.Initialize(arguments);
            vwState = VowpalWabbitState.Initialized;
        }

        void VWFinish()
        {
            if (vwState == VowpalWabbitState.Initialized)
            {
                VowpalWabbitInterface.Finish(vw);
                vwState = VowpalWabbitState.Finished;
            }
        }

        IntPtr vw;
        readonly object vwLock = new object();
        VowpalWabbitState vwState;

        BackgroundWorker worker;
        readonly CancellationTokenSource cancellationToken;

        readonly Action notifyPolicyUpdate;
        readonly string modelAddress;
        readonly string modelOutputDir;
        DateTimeOffset modelDate;

        readonly ManualResetEventSlim pollFinishedEvent;
    }

}
