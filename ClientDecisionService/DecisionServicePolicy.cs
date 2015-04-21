using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
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
        public DecisionServicePolicy(Action notifyPolicyUpdate, string modelAddress, string modelConnectionString, string modelOutputDir)
        {
            this.blobUpdater = new AzureBlobUpdater(this.ModelUpdate, "model", modelAddress, modelConnectionString, modelOutputDir);

            this.notifyPolicyUpdate = notifyPolicyUpdate;
        }

        public uint ChooseAction(TContext context)
        {
            string exampleLine = string.Format(CultureInfo.InvariantCulture, "1: | {0}", context);

            uint action = this.vw.Predict(exampleLine);

            return action;
        }

        public void StopPolling()
        {
            if (this.blobUpdater != null)
            {
                this.blobUpdater.StopPolling();
            }

            if (this.vw != null)
            {
                this.vw.Finish();
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

            this.vw.Finish();
        }

        void ModelUpdate(string modelFile)
        {
            bool modelUpdateSuccess = true;

            try
            {
                VowpalWabbitInstance oldVw = this.vw;
                this.vw = new VowpalWabbitInstance(string.Format(CultureInfo.InvariantCulture, "-t -i {0}", modelFile));
                oldVw.Finish();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unable to initialize VW.");
                Trace.TraceError(ex.ToString());
                modelUpdateSuccess = false;
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

        AzureBlobUpdater blobUpdater;

        VowpalWabbitInstance vw;

        readonly Action notifyPolicyUpdate;
    }

}
