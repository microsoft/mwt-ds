namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction
{
    using MultiWorldTesting.ExploreLibrary;
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
        readonly Action notifyPolicyUpdate;
        readonly string updateTaskId = "model";

        public DecisionServicePolicy(
            string modelAddress,
            string modelConnectionString, 
            string modelOutputDir,
            TimeSpan pollDelay,
            Action notifyPolicyUpdate,
            Action<Exception> modelPollFailureCallback,
            bool useJsonContext = false)
            : base(useJsonContext: useJsonContext)
        {
            if (useJsonContext)
            {
                if (typeof(TContext) != typeof(string))
                {
                    throw new InvalidOperationException("Type of context must be set to string since contexts were marked as Json format.");
                }
            }
            if (pollDelay != TimeSpan.MinValue)
            {
                AzureBlobUpdater.RegisterTask(this.updateTaskId, modelAddress,
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
            AzureBlobUpdater.CancelTask(this.updateTaskId);
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
    }
}

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction
{
    using MultiWorldTesting.ExploreLibrary;
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
        readonly Action notifyPolicyUpdate;
        readonly string updateTaskId = "model";

        public DecisionServicePolicy(
            string modelAddress,
            string modelConnectionString,
            string modelOutputDir,
            TimeSpan pollDelay,
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            Action notifyPolicyUpdate,
            Action<Exception> modelPollFailureCallback,
            bool useJsonContext = false)
            : base(getContextFeaturesFunc, useJsonContext: useJsonContext)
        {
            if (pollDelay != TimeSpan.MinValue)
            {
                AzureBlobUpdater.RegisterTask(this.updateTaskId, modelAddress,
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
            AzureBlobUpdater.CancelTask(this.updateTaskId);
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
    }

}