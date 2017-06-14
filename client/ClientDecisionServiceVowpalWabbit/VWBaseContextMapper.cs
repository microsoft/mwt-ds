using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using VW;
using VW.Serializer;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public abstract class VWBaseContextMapper<TVowpalWabbit, TContext, TAction>
        : IUpdatable<Stream>, IDisposable, IContextMapper<TContext, TAction>
        where TVowpalWabbit : class, IDisposable
    {
        protected ITypeInspector typeInspector;
        protected VowpalWabbitThreadedPredictionBase<TVowpalWabbit> vwPool;
        protected bool developmentMode;

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        protected VWBaseContextMapper(
            Stream vwModelStream = null,
            ITypeInspector typeInspector = null,
            bool developmentMode = false)
        {
            if (typeInspector == null)
                typeInspector = JsonTypeInspector.Default;
            this.typeInspector = typeInspector;
            this.developmentMode = developmentMode;
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
                new VowpalWabbitSettings
                {
                    ModelStream = modelStream
                });

            if (this.vwPool == null)
            {
                this.vwPool = this.CreatePool(new VowpalWabbitSettings
                {
                    MaxExampleCacheSize = 1024,
                    TypeInspector = this.typeInspector,
                    EnableStringExampleGeneration = this.developmentMode,
                    EnableStringFloatCompact = this.developmentMode
                });
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
                if (this.vwPool != null)
                {
                    this.vwPool.Dispose();
                    this.vwPool = null;
                }
            }
        }

        public Task<PolicyDecision<TAction>> MapContextAsync(TContext context)
        {
            if (this.vwPool == null)
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");

            using (var vw = this.vwPool.GetOrCreate())
            {
                if (vw.Value == null)
                    throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");

                return Task.FromResult(MapContext(vw.Value, context)); 
            }
        }

        protected abstract VowpalWabbitThreadedPredictionBase<TVowpalWabbit> CreatePool(VowpalWabbitSettings settings);

        protected abstract PolicyDecision<TAction> MapContext(TVowpalWabbit vw, TContext context);
    }
}
