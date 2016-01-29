namespace ClientDecisionService.SingleAction
{
    using MultiWorldTesting;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

    /// <summary>
    /// Represent an updatable <see cref="IPolicy<TContext>"/> object which can consume different VowpalWabbit 
    /// models to predict actions from an object of specified <see cref="TContext"/> type. This type 
    /// of object can also observe Azure Storage for newer model files.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    internal class DecisionServicePolicy<TContext> : VWPolicy<TContext>
    {
        public DecisionServicePolicy(
            string modelAddress,
            string modelConnectionString, 
            string modelOutputDir,
            TimeSpan pollDelay,
            Action<TContext, string> setModelIdCallback,
            Action notifyPolicyUpdate,
            Action<Exception> modelPollFailureCallback) : base(setModelIdCallback)
        {
            if (pollDelay != TimeSpan.MinValue)
            {
                this.blobUpdater = new AzureBlobUpdater("model", modelAddress,
                   modelConnectionString, modelOutputDir, pollDelay,
                   this.UpdateFromFile, modelPollFailureCallback);
            }

            this.notifyPolicyUpdate = notifyPolicyUpdate;
        }

        /// <summary>
        /// Stop checking for new model update.
        /// </summary>
        public void StopPolling()
        {
            if (this.blobUpdater != null)
            {
                this.blobUpdater.StopPolling();
            }
        }

        /// <summary>
        /// Dispose the object.
        /// </summary>
        /// <param name="disposing">Whether the object is disposing resources.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                // free managed resources
                if (this.blobUpdater != null)
                {
                    this.blobUpdater.Dispose();
                    this.blobUpdater = null;
                }
            }
        }

        /// <summary>
        /// Update new model from file and trigger callback if success.
        /// </summary>
        /// <param name="modelFile">The model file to load from.</param>
        /// <remarks>
        /// Triggered when a new model blob is found.
        /// </remarks>
        internal void UpdateFromFile(string modelFile)
        {
            if (base.ModelUpdate(modelFile) && this.notifyPolicyUpdate != null)
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

namespace ClientDecisionService.MultiAction
{
    using MultiWorldTesting;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

    /// <summary>
    /// Represent an updatable <see cref="IPolicy<TContext>"/> object which can consume different VowpalWabbit 
    /// models to predict actions from an object of specified <see cref="TContext"/> type. This type 
    /// of object can also observe Azure Storage for newer model files.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    internal class DecisionServicePolicy<TContext, TActionDependentFeature> : VWPolicy<TContext, TActionDependentFeature>
    {
        public DecisionServicePolicy(
            string modelAddress,
            string modelConnectionString,
            string modelOutputDir,
            TimeSpan pollDelay,
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            Action<TContext, string> setModelIdCallback,
            Action notifyPolicyUpdate,
            Action<Exception> modelPollFailureCallback)
            : base(getContextFeaturesFunc, setModelIdCallback)
        {
            if (pollDelay != TimeSpan.MinValue)
            {
                this.blobUpdater = new AzureBlobUpdater("model", modelAddress,
                   modelConnectionString, modelOutputDir, pollDelay,
                   this.UpdateFromFile, modelPollFailureCallback);
            }

            this.notifyPolicyUpdate = notifyPolicyUpdate;
        }

        /// <summary>
        /// Stop checking for new model update.
        /// </summary>
        public void StopPolling()
        {
            if (this.blobUpdater != null)
            {
                this.blobUpdater.StopPolling();
            }
        }

        /// <summary>
        /// Dispose the object.
        /// </summary>
        /// <param name="disposing">Whether the object is disposing resources.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                // free managed resources
                if (this.blobUpdater != null)
                {
                    this.blobUpdater.Dispose();
                    this.blobUpdater = null;
                }
            }
        }

        /// <summary>
        /// Update new model from file and trigger callback if success.
        /// </summary>
        /// <param name="modelFile">The model file to load from.</param>
        /// <remarks>
        /// Triggered when a new model blob is found.
        /// </remarks>
        internal void UpdateFromFile(string modelFile)
        {
            if (base.ModelUpdate(modelFile) && this.notifyPolicyUpdate != null)
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