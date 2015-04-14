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
            this.blobUpdater.StopPolling();

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
                if (this.blobUpdater != null)
                {
                    this.blobUpdater.Dispose();
                    this.blobUpdater = null;
                }
            }

            lock (vwLock)
            {
                this.VWFinish();
            }
        }

        void ModelUpdate(string modelFile)
        {
            bool modelUpdateSuccess = true;
            lock (vwLock)
            {
                try
                {
                    this.VWFinish(); // Finish previous run before initializing on new file
                    this.VWInitialize(string.Format(CultureInfo.InvariantCulture, "-t -i {0}", modelFile));
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

        AzureBlobUpdater blobUpdater;

        IntPtr vw;
        readonly object vwLock = new object();
        VowpalWabbitState vwState;

        readonly Action notifyPolicyUpdate;
    }

}
