using MultiWorldTesting;
using System;
using System.ComponentModel;
using System.Net;
using System.Threading;

namespace DecisionSample
{
    internal class DecisionServicePolicy<TContext> : IPolicy<TContext>, IDisposable
    {
        public DecisionServicePolicy(Action notifyPolicyUpdate, string modelAddress)
        {
            this.notifyPolicyUpdate = notifyPolicyUpdate;
            this.modelAddress = modelAddress;

            this.cancellationToken = new CancellationTokenSource();

            this.worker = new BackgroundWorker();
            this.worker.DoWork += PollForUpdate;
            this.worker.ProgressChanged += FoundUpdate;
            this.worker.RunWorkerAsync(this.cancellationToken);
        }

        public uint ChooseAction(TContext context)
        {
            // TODO: how to create an example from just the context?
            string exampleLine = string.Empty;

            IntPtr example = VowpalWabbitInterface.ReadExample(vw, exampleLine);
            VowpalWabbitInterface.Predict(vw, example);
            VowpalWabbitInterface.FinishExample(vw, example);

            return (uint)VowpalWabbitInterface.GetCostSensitivePrediction(example);
        }

        public void StopPolling()
        {
            this.cancellationToken.Cancel();
            this.VWFinish();
        }

        public void Dispose() { }

        void FoundUpdate(object sender, ProgressChangedEventArgs e)
        {
            var newModelFileName = e.UserState as string;
            
            this.VWFinish(); // Finish previous run before initializing on new file
            this.VWInitialize(string.Format("-t -i {0}", newModelFileName));
            
            this.notifyPolicyUpdate();
        }

        void PollForUpdate(object sender, DoWorkEventArgs e)
        {
            var cancelToken = e.Argument as CancellationTokenSource;
            while (!cancelToken.IsCancellationRequested)
            {
                System.Threading.Thread.Sleep(PollDelayInMiliseconds);

                using (var webClient = new WebClient())
                {
                    // TODO: check if there is an update before downloading?
                    string modelBase64 = webClient.DownloadString(modelAddress);
                    byte[] modelBytes = Convert.FromBase64String(modelBase64);

                    // TODO: use name of the Azure blob file instead
                    string modelFileName = Guid.NewGuid().ToString();
                    System.IO.File.WriteAllBytes(modelFileName, modelBytes);

                    worker.ReportProgress(0, modelFileName);
                }
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
        VowpalWabbitState vwState;

        BackgroundWorker worker;
        CancellationTokenSource cancellationToken;
        
        Action notifyPolicyUpdate;
        string modelAddress;

        #region Constants

        private readonly int PollDelayInMiliseconds = 5000;

        #endregion
    }

}
