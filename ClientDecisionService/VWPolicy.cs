namespace ClientDecisionService.SingleAction
{
    using MultiWorldTesting.SingleAction;
    using System;
    using System.Diagnostics;
    using System.IO;
    using VW;

    /// <summary>
    /// Represent an updatable <see cref="IPolicy<TContext>"/> object which can consume different VowpalWabbit 
    /// models to predict a list of actions from an object of specified <see cref="TContext"/> type.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    public class VWPolicy<TContext> : IPolicy<TContext>, IDisposable
    {
        /// <summary>
        /// Constructor using an optional model file.
        /// </summary>
        /// <param name="setModelIdCallback">Callback to set model id in the Context for reproducibility.</param>
        /// <param name="vwModelFile">Optional; the VowpalWabbit model file to load from.</param>
        public VWPolicy(Action<TContext, string> setModelIdCallback, string vwModelFile = null)
        {
            this.setModelIdCallback = setModelIdCallback;
            if (vwModelFile != null)
            {
                this.ModelUpdate(vwModelFile);
            }
        }

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="setModelIdCallback">Callback to set model id in the Context for reproducibility.</param>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        public VWPolicy(Action<TContext, string> setModelIdCallback, Stream vwModelStream)
        {
            this.setModelIdCallback = setModelIdCallback;
            this.ModelUpdate(vwModelStream);
        }

        /// <summary>
        /// Scores the model against the specified context and returns the chosen action (1-based index).
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned integer representing the chosen action.</returns>
        public virtual uint ChooseAction(TContext context, uint numActionsVariable = uint.MaxValue)
        {
            if (vwPool == null)
            {
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
            }
            using (var vw = vwPool.GetOrCreate())
            {
                this.setModelIdCallback(context, vw.Value.Native.ID);

                return (uint)vw.Value.Predict(context, VowpalWabbitPredictionType.CostSensitive);
            }
        }

        /// <summary>
        /// Update VW model from file.
        /// </summary>
        /// <param name="modelFile">The model file to load.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(string modelFile)
        {
            return ModelUpdate(() => { return new VowpalWabbitModel(new VowpalWabbitSettings(string.Format("--quiet -t -i {0}", modelFile), maxExampleCacheSize: 1024)); });
        }

        /// <summary>
        /// Update VW model from stream.
        /// </summary>
        /// <param name="modelStream">The model stream to load from.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Stream modelStream)
        {
            return ModelUpdate(() => new VowpalWabbitModel(new VowpalWabbitSettings("--quiet -t", modelStream: modelStream, maxExampleCacheSize: 1024)));
        }

        /// <summary>
        /// Update VW model using a generic method which loads the model.
        /// </summary>
        /// <param name="loadModelFunc">The generic method to load the model.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Func<VowpalWabbitModel> loadModelFunc)
        {
            VowpalWabbitModel vwModel = loadModelFunc();

            if (this.vwPool == null)
            {
                this.vwPool = new VowpalWabbitThreadedPrediction<TContext>(vwModel);
            }
            else
            {
                this.vwPool.UpdateModel(vwModel);
            }
            return true;
        }

        /// <summary>
        /// Dispose the object and clean up any resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose the object.
        /// </summary>
        /// <param name="disposing">Whether the object is disposing resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.vwPool != null)
                {
                    this.vwPool.Dispose();
                    this.vwPool = null;
                }
            }
        }

        protected VowpalWabbitThreadedPrediction<TContext> vwPool;
        protected Action<TContext, string> setModelIdCallback;
    }
}

namespace ClientDecisionService.MultiAction
{
    using VW;
    using VW.Interfaces;
    using MultiWorldTesting.MultiAction;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.IO;
    using System.Collections.Generic;

    /// <summary>
    /// Represent an updatable <see cref="IPolicy<TContext>"/> object which can consume different VowpalWabbit 
    /// models to predict a list of actions from an object of specified <see cref="TContext"/> type.
    /// </summary>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    public class VWPolicy<TContext, TActionDependentFeature> : IPolicy<TContext>, IDisposable
    {
        /// <summary>
        /// Constructor using an optional model file.
        /// </summary>
        /// <param name="getContextFeaturesFunc">Callback to get features from the Context.</param>
        /// <param name="setModelIdCallback">Callback to set model id in the Context for reproducibility.</param>
        /// <param name="vwModelFile">Optional; the VowpalWabbit model file to load from.</param>
        public VWPolicy(
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            Action<TContext, string> setModelIdCallback,
            string vwModelFile = null)
        {
            this.getContextFeaturesFunc = getContextFeaturesFunc;
            this.setModelIdCallback = setModelIdCallback;
            if (vwModelFile != null)
            {
                this.ModelUpdate(vwModelFile);
            }
        }

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="getContextFeaturesFunc">Callback to get features from the Context.</param>
        /// <param name="setModelIdCallback">Callback to set model id in the Context for reproducibility.</param>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        public VWPolicy(
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            Action<TContext, string> setModelIdCallback,
            Stream vwModelStream)
        {
            this.getContextFeaturesFunc = getContextFeaturesFunc;
            this.setModelIdCallback = setModelIdCallback;
            this.ModelUpdate(vwModelStream);
        }

        /// <summary>
        /// Scores the model against the specified context and returns a list of actions (1-based index).
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>List of predicted actions.</returns>
        public virtual uint[] ChooseAction(TContext context, uint numActionsVariable = uint.MaxValue)
        {
            if (vwPool == null)
            {
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
            }
            using (var vw = vwPool.GetOrCreate())
            {
                IReadOnlyCollection<TActionDependentFeature> features = this.getContextFeaturesFunc(context);

                // return indices
                ActionDependentFeature<TActionDependentFeature>[] vwMultilabelPredictions = vw.Value.Predict(context, features);

                // Callback to store model Id in the Context
                this.setModelIdCallback(context, vw.Value.Native.ID);

                // VW multi-label predictions are 0-based
                return vwMultilabelPredictions.Select(p => (uint)(p.Index + 1)).ToArray();
            }
        }

        /// <summary>
        /// Update VW model from file.
        /// </summary>
        /// <param name="modelFile">The model file to load.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(string modelFile)
        {
            return ModelUpdate(() => { return new VowpalWabbitModel(new VowpalWabbitSettings(string.Format("--quiet -t -i {0}", modelFile), maxExampleCacheSize: 1024)); });
        }

        /// <summary>
        /// Update VW model from stream.
        /// </summary>
        /// <param name="modelStream">The model stream to load from.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Stream modelStream)
        {
            return ModelUpdate(() => new VowpalWabbitModel(new VowpalWabbitSettings("--quiet -t", modelStream: modelStream, maxExampleCacheSize: 1024)));
        }

        /// <summary>
        /// Update VW model using a generic method which loads the model.
        /// </summary>
        /// <param name="loadModelFunc">The generic method to load the model.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Func<VowpalWabbitModel> loadModelFunc)
        {
            VowpalWabbitModel vwModel = loadModelFunc();

            if (this.vwPool == null)
            {
                this.vwPool = new VowpalWabbitThreadedPrediction<TContext, TActionDependentFeature>(vwModel);
            }
            else
            {
                this.vwPool.UpdateModel(vwModel);
            }
            return true;
        }

        /// <summary>
        /// Dispose the object and clean up any resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose the object.
        /// </summary>
        /// <param name="disposing">Whether the object is disposing resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.vwPool != null)
                {
                    this.vwPool.Dispose();
                    this.vwPool = null;
                }
            }
        }

        protected VowpalWabbitThreadedPrediction<TContext, TActionDependentFeature> vwPool;
        protected Action<TContext, string> setModelIdCallback;
        private Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc;
    }
}