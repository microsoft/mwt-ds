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
        public VWPolicy(
            string vwModelFile = null,
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Default)
        {
            this.featureDiscovery = featureDiscovery;
            if (vwModelFile != null)
            {
                this.ModelUpdate(vwModelFile);
            }
        }

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        public VWPolicy(
            Stream vwModelStream,
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Default)
        {
            this.featureDiscovery = featureDiscovery;
            this.ModelUpdate(vwModelStream);
        }

        /// <summary>
        /// Scores the model against the specified context and returns the chosen action (1-based index).
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned integer representing the chosen action.</returns>
        public virtual PolicyDecisionTuple ChooseAction(TContext context, uint numActionsVariable = uint.MaxValue)
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

        /// <summary>
        /// Update VW model from file.
        /// </summary>
        /// <param name="modelFile">The model file to load.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(string modelFile)
        {
            return ModelUpdate(() => { return new VowpalWabbitModel(
                new VowpalWabbitSettings(
                    string.Format("--quiet -t -i {0}", modelFile),
                    maxExampleCacheSize: 1024,
                    featureDiscovery: this.featureDiscovery)); });
        }

        /// <summary>
        /// Update VW model from stream.
        /// </summary>
        /// <param name="modelStream">The model stream to load from.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Stream modelStream)
        {
            return ModelUpdate(() => new VowpalWabbitModel(
                new VowpalWabbitSettings(
                    "--quiet -t",
                    modelStream: modelStream,
                    maxExampleCacheSize: 1024,
                    featureDiscovery: this.featureDiscovery)));
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
        private VowpalWabbitFeatureDiscovery featureDiscovery;
    }

    public class VWJsonPolicy : IPolicy<string>, IDisposable
    {
        public VWJsonPolicy(string vwModelFile = null)
        {
            if (vwModelFile != null)
            {
                this.ModelUpdate(vwModelFile);
            }
        }

        public VWJsonPolicy(Stream vwModelStream)
        {
            this.ModelUpdate(vwModelStream);
        }

        public virtual PolicyDecisionTuple ChooseAction(string context, uint numActionsVariable = uint.MaxValue)
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

        public bool ModelUpdate(string modelFile)
        {
            return ModelUpdate(() => { return new VowpalWabbitModel(new VowpalWabbitSettings(string.Format("--quiet -t -i {0}", modelFile), maxExampleCacheSize: 1024)); });
        }

        public bool ModelUpdate(Stream modelStream)
        {
            return ModelUpdate(() => new VowpalWabbitModel(new VowpalWabbitSettings("--quiet -t", modelStream: modelStream, maxExampleCacheSize: 1024)));
        }

        public bool ModelUpdate(Func<VowpalWabbitModel> loadModelFunc)
        {
            VowpalWabbitModel vwModel = loadModelFunc();

            if (this.vwJsonPool == null)
            {
                this.vwJsonPool = new VowpalWabbitThreadedPrediction(vwModel);
            }
            else
            {
                this.vwJsonPool.UpdateModel(vwModel);
            }

            return true;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.vwJsonPool != null)
                {
                    this.vwJsonPool.Dispose();
                    this.vwJsonPool = null;
                }
            }
        }

        protected VowpalWabbitThreadedPrediction vwJsonPool;
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
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Default)
        {
            this.featureDiscovery = featureDiscovery;
            this.getContextFeaturesFunc = getContextFeaturesFunc;
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
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Default)
        {
            this.featureDiscovery = featureDiscovery;
            this.getContextFeaturesFunc = getContextFeaturesFunc;
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
            if (vwMultiPool == null)
            {
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
            }
            using (var vw = vwMultiPool.GetOrCreate())
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

        /// <summary>
        /// Update VW model from file.
        /// </summary>
        /// <param name="modelFile">The model file to load.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(string modelFile)
        {
            return ModelUpdate(() => { return new VowpalWabbitModel(
                new VowpalWabbitSettings(
                    string.Format("--quiet -t -i {0}", modelFile),
                    featureDiscovery: this.featureDiscovery,
                    maxExampleCacheSize: 1024)); 
            });
        }

        /// <summary>
        /// Update VW model from stream.
        /// </summary>
        /// <param name="modelStream">The model stream to load from.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Stream modelStream)
        {
            return ModelUpdate(() => new VowpalWabbitModel(
                new VowpalWabbitSettings(
                    "--quiet -t",
                    featureDiscovery: this.featureDiscovery,
                    modelStream: modelStream,
                    maxExampleCacheSize: 1024))
            );
        }

        /// <summary>
        /// Update VW model using a generic method which loads the model.
        /// </summary>
        /// <param name="loadModelFunc">The generic method to load the model.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Func<VowpalWabbitModel> loadModelFunc)
        {
            VowpalWabbitModel vwModel = loadModelFunc();

            if (this.vwMultiPool == null)
            {
                this.vwMultiPool = new VowpalWabbitThreadedPrediction<TContext, TActionDependentFeature>(vwModel);
            }
            else
            {
                this.vwMultiPool.UpdateModel(vwModel);
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
                if (this.vwMultiPool != null)
                {
                    this.vwMultiPool.Dispose();
                    this.vwMultiPool = null;
                }
                if (this.vwSinglePool != null)
                {
                    this.vwSinglePool.Dispose();
                    this.vwSinglePool = null;
                }
            }
        }

        protected VowpalWabbitThreadedPrediction<TContext> vwSinglePool;
        protected VowpalWabbitThreadedPrediction<TContext, TActionDependentFeature> vwMultiPool;
        private Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc;
        private VowpalWabbitFeatureDiscovery featureDiscovery;
    }

    public class VWJsonDirectPolicy<TContext> : IPolicy<TContext>, IDisposable
    {
        public VWJsonDirectPolicy(
            string vwModelFile = null)
        {
            if (vwModelFile != null)
            {
                this.ModelUpdate(vwModelFile);
            }
        }

        public VWJsonDirectPolicy(
            Stream vwModelStream,
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Default)
        {
            this.ModelUpdate(vwModelStream);
        }

        public virtual PolicyDecisionTuple ChooseAction(TContext context, uint numActionsVariable = uint.MaxValue)
        {
            if (vwPool == null)
            {
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
            }
            using (var vw = vwPool.GetOrCreate())
            {
                // return indices
                int[] vwMultilabelPredictions = vw.Value.Predict(context, VowpalWabbitPredictionType.Multilabel);
                Console.Write(".");
                // VW multi-label predictions are 0-based
                return new PolicyDecisionTuple
                {
                    Actions = vwMultilabelPredictions.Select(p => (uint)(p + 1)).ToArray(),
                    ModelId = vw.Value.Native.ID
                };
            }
        }

        public bool ModelUpdate(string modelFile)
        {
            return ModelUpdate(() =>
            {
                return new VowpalWabbitModel(
                    new VowpalWabbitSettings(
                    string.Format("--quiet -t -i {0}", modelFile),
                    featureDiscovery: VowpalWabbitFeatureDiscovery.Json,
                    maxExampleCacheSize: 1024));
            });
        }

        public bool ModelUpdate(Stream modelStream)
        {
            return ModelUpdate(() => new VowpalWabbitModel(
                new VowpalWabbitSettings(
                    "--quiet -t",
                    featureDiscovery: VowpalWabbitFeatureDiscovery.Json,
                    modelStream: modelStream,
                    maxExampleCacheSize: 1024))
            );
        }

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

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

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
    }

    public class VWJsonPolicy<TActionDependentFeature> : IPolicy<string>, IDisposable
    {
        public VWJsonPolicy(
            Func<string, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            string vwModelFile = null)
        {
            this.getContextFeaturesFunc = getContextFeaturesFunc;
            if (vwModelFile != null)
            {
                this.ModelUpdate(vwModelFile);
            }
        }

        public VWJsonPolicy(
            Func<string, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            Stream vwModelStream)
        {
            this.getContextFeaturesFunc = getContextFeaturesFunc;
            this.ModelUpdate(vwModelStream);
        }

        public virtual PolicyDecisionTuple ChooseAction(string context, uint numActionsVariable = uint.MaxValue)
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

        public bool ModelUpdate(string modelFile)
        {
            return ModelUpdate(() => { return new VowpalWabbitModel(new VowpalWabbitSettings(string.Format("--quiet -t -i {0}", modelFile), maxExampleCacheSize: 1024)); });
        }

        public bool ModelUpdate(Stream modelStream)
        {
            return ModelUpdate(() => new VowpalWabbitModel(new VowpalWabbitSettings("--quiet -t", modelStream: modelStream, maxExampleCacheSize: 1024)));
        }

        public bool ModelUpdate(Func<VowpalWabbitModel> loadModelFunc)
        {
            VowpalWabbitModel vwModel = loadModelFunc();

            if (this.vwJsonPool == null)
            {
                this.vwJsonPool = new VowpalWabbitThreadedPrediction(vwModel);
            }
            else
            {
                this.vwJsonPool.UpdateModel(vwModel);
            }

            return true;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.vwJsonPool != null)
                {
                    this.vwJsonPool.Dispose();
                    this.vwJsonPool = null;
                }
            }
        }

        protected VowpalWabbitThreadedPrediction vwJsonPool;
        private Func<string, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc;
    }

}