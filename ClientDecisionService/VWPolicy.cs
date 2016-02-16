namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction
{
    using MultiWorldTesting.ExploreLibrary.SingleAction;
    using System;
    using System.Diagnostics;
    using System.IO;
    using VW;
    using VW.Serializer;

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
        /// <param name="vwModelFile">Optional; the VowpalWabbit model file to load from.</param>
        public VWPolicy(string vwModelFile = null, bool useJsonContext = false)
        {
            if (vwModelFile != null)
            {
                this.ModelUpdate(vwModelFile);
            }
            this.useJsonContext = useJsonContext;
        }

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        public VWPolicy(Stream vwModelStream, bool useJsonContext = false)
        {
            this.ModelUpdate(vwModelStream);
            this.useJsonContext = useJsonContext;
        }

        /// <summary>
        /// Scores the model against the specified context and returns the chosen action (1-based index).
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned integer representing the chosen action.</returns>
        public virtual PolicyDecisionTuple ChooseAction(TContext context, uint numActionsVariable = uint.MaxValue)
        {
            if (this.useJsonContext)
            {
                if (vwJsonPool == null)
                {
                    throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
                }
                using (var vw = vwJsonPool.GetOrCreate())
                {
                    var vwJson = new VowpalWabbitJsonSerializer(vw.Value);
                    using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context as string))
                    {
                        return new PolicyDecisionTuple
                        {
                            Action = (uint)vwExample.Predict(VowpalWabbitPredictionType.CostSensitive),
                            ModelId = vw.Value.ID
                        };
                    }
                }
            }
            else
            {
                if (vwPool == null)
                {
                    throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
                }
                using (var vw = vwPool.GetOrCreate())
                {
                    return new PolicyDecisionTuple
                    {
                        Action = (uint)vw.Value.Predict(context, VowpalWabbitPredictionType.CostSensitive),
                        ModelId = vw.Value.Native.ID
                    };
                }
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

            if (this.useJsonContext)
            {
                if (this.vwJsonPool == null)
                {
                    this.vwJsonPool = new VowpalWabbitThreadedPrediction(vwModel);
                }
                else
                {
                    this.vwJsonPool.UpdateModel(vwModel);
                }
            }
            else
            {
                if (this.vwPool == null)
                {
                    this.vwPool = new VowpalWabbitThreadedPrediction<TContext>(vwModel);
                }
                else
                {
                    this.vwPool.UpdateModel(vwModel);
                }
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
                if (this.vwJsonPool != null)
                {
                    this.vwJsonPool.Dispose();
                    this.vwJsonPool = null;
                }
            }
        }

        protected VowpalWabbitThreadedPrediction<TContext> vwPool;
        protected VowpalWabbitThreadedPrediction vwJsonPool;
        private bool useJsonContext;
    }
}

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction
{
    using VW;
    using VW.Interfaces;
    using MultiWorldTesting.ExploreLibrary.MultiAction;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.IO;
    using System.Collections.Generic;
    using VW.Serializer;

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
        /// <param name="vwModelFile">Optional; the VowpalWabbit model file to load from.</param>
        public VWPolicy(
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            string vwModelFile = null,
            bool useJsonContext = false)
        {
            this.getContextFeaturesFunc = getContextFeaturesFunc;
            this.useJsonContext = useJsonContext;
            if (vwModelFile != null)
            {
                this.ModelUpdate(vwModelFile);
            }
        }

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="getContextFeaturesFunc">Callback to get features from the Context.</param>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        public VWPolicy(
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            Stream vwModelStream,
            bool useJsonContext = false)
        {
            this.getContextFeaturesFunc = getContextFeaturesFunc;
            this.useJsonContext = useJsonContext;
            this.ModelUpdate(vwModelStream);
        }

        /// <summary>
        /// Scores the model against the specified context and returns a list of actions (1-based index).
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>List of predicted actions.</returns>
        public virtual PolicyDecisionTuple ChooseAction(TContext context, uint numActionsVariable = uint.MaxValue)
        {
            if (this.useJsonContext)
            {
                if (vwJsonPool == null)
                {
                    throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
                }
                using (var vw = vwJsonPool.GetOrCreate())
                {
                    var vwJson = new VowpalWabbitJsonSerializer(vw.Value);
                    using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context as string))
                    {
                        int[] vwMultilabelPredictions = vwExample.Predict(VowpalWabbitPredictionType.Multilabel);

                        // VW multi-label predictions are 0-based
                        return new PolicyDecisionTuple
                        {
                            Actions = vwMultilabelPredictions.Select(a => (uint)(a + 1)).ToArray(),
                            ModelId = vw.Value.ID
                        };
                    }
                }
            }
            else
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

                    // VW multi-label predictions are 0-based
                    return new PolicyDecisionTuple
                    {
                        Actions = vwMultilabelPredictions.Select(p => (uint)(p.Index + 1)).ToArray(),
                        ModelId = vw.Value.Native.ID
                    };
                }
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

            if (this.useJsonContext)
            {
                if (this.vwJsonPool == null)
                {
                    this.vwJsonPool = new VowpalWabbitThreadedPrediction(vwModel);
                }
                else
                {
                    this.vwJsonPool.UpdateModel(vwModel);
                }
            }
            else
            {
                if (this.vwPool == null)
                {
                    this.vwPool = new VowpalWabbitThreadedPrediction<TContext, TActionDependentFeature>(vwModel);
                }
                else
                {
                    this.vwPool.UpdateModel(vwModel);
                }
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
                if (this.vwJsonPool != null)
                {
                    this.vwJsonPool.Dispose();
                    this.vwJsonPool = null;
                }
            }
        }

        protected VowpalWabbitThreadedPrediction<TContext, TActionDependentFeature> vwPool;
        protected VowpalWabbitThreadedPrediction vwJsonPool;
        private Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc;
        private bool useJsonContext;
    }
}