using MultiWorldTesting;
using System;
using System.Diagnostics;
using System.Globalization;

namespace ClientDecisionService
{
    internal class DecisionServicePolicy<TContext> : VWPolicy<TContext>
    {
        public DecisionServicePolicy(string modelAddress, string modelConnectionString, 
            string modelOutputDir, TimeSpan pollDelay, 
            Action notifyPolicyUpdate, Action<Exception> modelPollFailureCallback)
        {
            if (pollDelay != TimeSpan.MinValue)
            {
                this.blobUpdater = new AzureBlobUpdater("model", modelAddress,
                   modelConnectionString, modelOutputDir, pollDelay,
                   this.ModelUpdate, modelPollFailureCallback);
            }

            this.notifyPolicyUpdate = notifyPolicyUpdate;
        }

        public void StopPolling()
        {
            if (this.blobUpdater != null)
            {
                this.blobUpdater.StopPolling();
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
                if (this.blobUpdater != null)
                {
                    this.blobUpdater.Dispose();
                    this.blobUpdater = null;
                }
            }

            base.Dispose(true);
        }

        void ModelUpdate(string modelFile)
        {
            if (base.ModelUpdate(modelFile))
            {
                this.notifyPolicyUpdate();
            }
            else
            {
                Trace.TraceInformation("Attempt to update model failed.");
            }
        }

        AzureBlobUpdater blobUpdater;

        readonly Action notifyPolicyUpdate;
    }

}
