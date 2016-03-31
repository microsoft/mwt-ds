using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VW;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public abstract class VWBaseContextMapper<TPool, TVowpalWabbit, TContext, TValue>
        : IUpdatable<Stream>, IDisposable, IContextMapper<TContext, TValue>
        where TPool : VowpalWabbitThreadedPredictionBase<TVowpalWabbit>, new()
        where TVowpalWabbit : class, IDisposable
    {
        private VowpalWabbitFeatureDiscovery featureDiscovery;
        protected TPool vwPool;

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        public VWBaseContextMapper(
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

        public Decision<TValue> MapContext(TContext context, ref uint numActionsVariable)
        {
            if (this.vwPool == null)
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");

            using (var vw = this.vwPool.GetOrCreate())
            {
                if (vw.Value == null)
                    throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");

                return MapContext(vw.Value, context, ref numActionsVariable);    
            }
        }

        protected abstract Decision<TValue> MapContext(TVowpalWabbit vw, TContext context, ref uint numActionsVariable);
    }
}
