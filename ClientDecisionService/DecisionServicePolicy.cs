namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction
{
    using MultiWorldTesting.ExploreLibrary;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using VW;
    
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
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Default)
            : base(featureDiscovery: featureDiscovery)
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

    internal class DecisionServiceJsonPolicy : VWJsonPolicy
    {
        readonly Action notifyPolicyUpdate;
        readonly string updateTaskId = "model";

        public DecisionServiceJsonPolicy(
            string modelAddress,
            string modelConnectionString,
            string modelOutputDir,
            TimeSpan pollDelay,
            Action notifyPolicyUpdate,
            Action<Exception> modelPollFailureCallback)
        {
            if (pollDelay != TimeSpan.MinValue)
            {
                AzureBlobUpdater.RegisterTask(this.updateTaskId, modelAddress,
                   modelConnectionString, modelOutputDir, pollDelay,
                   this.UpdateFromFile, modelPollFailureCallback);
            }

            this.notifyPolicyUpdate = notifyPolicyUpdate;
        }

        public void StopPolling()
        {
            AzureBlobUpdater.CancelTask(this.updateTaskId);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                // free managed resources
            }
        }

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
    using VW;

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
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Default)
            : base(getContextFeaturesFunc, featureDiscovery: featureDiscovery)
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

    internal class DecisionServiceJsonDirectPolicy<TContext> : VWJsonDirectPolicy<TContext>
    {
        readonly Action notifyPolicyUpdate;
        readonly string updateTaskId = "model";

        public DecisionServiceJsonDirectPolicy(
            string modelAddress,
            string modelConnectionString,
            string modelOutputDir,
            TimeSpan pollDelay,
            Action notifyPolicyUpdate,
            Action<Exception> modelPollFailureCallback)
        {
            if (pollDelay != TimeSpan.MinValue)
            {
                AzureBlobUpdater.RegisterTask(this.updateTaskId, modelAddress,
                   modelConnectionString, modelOutputDir, pollDelay,
                   this.UpdateFromFile, modelPollFailureCallback);
            }

            this.notifyPolicyUpdate = notifyPolicyUpdate;
        }

        public void StopPolling()
        {
            AzureBlobUpdater.CancelTask(this.updateTaskId);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                // free managed resources
            }
        }

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

    internal class DecisionServiceJsonPolicy<TActionDependentFeature> : VWJsonPolicy<TActionDependentFeature>
    {
        readonly Action notifyPolicyUpdate;
        readonly string updateTaskId = "model";

        public DecisionServiceJsonPolicy(
            string modelAddress,
            string modelConnectionString,
            string modelOutputDir,
            TimeSpan pollDelay,
            Func<string, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            Action notifyPolicyUpdate,
            Action<Exception> modelPollFailureCallback)
            : base(getContextFeaturesFunc)
        {
            if (pollDelay != TimeSpan.MinValue)
            {
                AzureBlobUpdater.RegisterTask(this.updateTaskId, modelAddress,
                   modelConnectionString, modelOutputDir, pollDelay,
                   this.UpdateFromFile, modelPollFailureCallback);
            }

            this.notifyPolicyUpdate = notifyPolicyUpdate;
        }

        public void StopPolling()
        {
            AzureBlobUpdater.CancelTask(this.updateTaskId);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                // free managed resources
            }
        }

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