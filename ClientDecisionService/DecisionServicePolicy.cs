using MultiWorldTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

            this.worker = new BackgroundWorker();
            this.worker.WorkerReportsProgress = true;
            this.worker.DoWork += PollForUpdate;
            this.worker.ProgressChanged += FoundUpdate;
            this.worker.RunWorkerAsync(this.cancellationToken);
        }

        public uint ChooseAction(TContext context)
        {
            // Create example with bogus <a,r,p> data
            string exampleLine = string.Format("1:1:1 | {0}", context.ToString());

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

            // TODO: use more robust mechanism to wait for background worker to finish
            // Blocks until the worker can gracefully exit.
            var waitWatch = new Stopwatch();
            waitWatch.Start();
            while (this.worker.IsBusy) 
            {
                if (waitWatch.ElapsedMilliseconds >= PollCancelWaitInMiliseconds)
                {
                    Trace.TraceWarning("Timed out waiting for model polling task: {0} ms.", PollCancelWaitInMiliseconds);
                    break;
                }
            }

            lock (vwLock)
            {
                this.VWFinish();
            }
        }

        public void Dispose() { }

        void FoundUpdate(object sender, ProgressChangedEventArgs e)
        {
            var newModelFileName = e.UserState as string;

            bool modelUpdateSuccess = true;
            lock (vwLock)
            {
                try
                {
                    this.VWFinish(); // Finish previous run before initializing on new file
                    this.VWInitialize(string.Format("-t -i {0}", Path.Combine(this.modelOutputDir, newModelFileName)));
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
            while (!cancelToken.IsCancellationRequested)
            {
                bool cancelled = cancelToken.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(PollDelayInMiliseconds));
                if (cancelled)
                {
                    Trace.TraceInformation("Cancellation request received while sleeping.");
                    return;
                }

                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(modelAddress);
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
                        HttpWebResponse httpResponse = ((WebException)ex).Response as HttpWebResponse;
                        
                        switch (httpResponse.StatusCode)
                        {
                            case HttpStatusCode.NotModified:
                                // Exception is raised for NotModified http response but this is expected.
                                Trace.TraceInformation("No new model found.");
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
            Trace.TraceInformation("Model polling has been cancelled per request.");
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
        CancellationTokenSource cancellationToken;
        
        Action notifyPolicyUpdate;
        string modelAddress;
        string modelOutputDir;
        DateTimeOffset modelDate;

        #region Constants

        // TODO: Configurable?
        private readonly int PollDelayInMiliseconds = 5000;
        private readonly int PollCancelWaitInMiliseconds = 2000;

        #endregion
    }

}
