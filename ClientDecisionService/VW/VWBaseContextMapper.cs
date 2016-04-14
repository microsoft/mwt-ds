using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;
using VW;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public abstract class VWBaseContextMapper<TPool, TVowpalWabbit, TContext, TAction>
        : IUpdatable<Stream>, IDisposable, IContextMapper<TContext, TAction>
        where TPool : VowpalWabbitThreadedPredictionBase<TVowpalWabbit>, new()
        where TVowpalWabbit : class, IDisposable
    {
        private VowpalWabbitFeatureDiscovery featureDiscovery;
        protected TPool vwPool;

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        protected VWBaseContextMapper(
            Stream vwModelStream = null,
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Default)
        {
            this.featureDiscovery = featureDiscovery;
            this.Update(vwModelStream);
        }

        /// <summary>
        /// Update VW model from stream.
        /// </summary>
        /// <param name="modelStream">The model stream to load from.</param>
        public void Update(Stream modelStream)
        {
            if (modelStream == null)
                return;

            var model = new VowpalWabbitModel(
                new VowpalWabbitSettings(
                    "--quiet -t",
                    modelStream: modelStream,
                    maxExampleCacheSize: 1024,
                    featureDiscovery: this.featureDiscovery));

            if (this.vwPool == null)
            {
                this.vwPool = new TPool();
                this.vwPool.UpdateModel(model);
            }
            else
                this.vwPool.UpdateModel(model);
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
                if (this.vwPool != default(TPool))
                {
                    this.vwPool.Dispose();
                    this.vwPool = null;
                }
            }
        }

        public PolicyDecision<TAction> MapContext(TContext context)
        {
            if (this.vwPool == null)
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");

            using (var vw = this.vwPool.GetOrCreate())
            {
                if (vw.Value == null)
                    throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");

                return MapContext(vw.Value, context);    
            }
        }

        protected abstract PolicyDecision<TAction> MapContext(TVowpalWabbit vw, TContext context);
    }
}
