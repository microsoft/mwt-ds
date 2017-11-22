using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using VW;
using VW.Serializer;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Base class for all Vowpal Wabbit contenxt mappers.
    /// </summary>
    public abstract class VWBaseContextMapper<TVowpalWabbit, TContext, TAction>
        : IUpdatable<Stream>, IDisposable, IContextMapper<TContext, TAction>
        where TVowpalWabbit : class, IDisposable
    {
        /// <summary>
        /// Type inspector used to extract schema information.
        /// </summary>
        protected ITypeInspector typeInspector;

        /// <summary>
        /// The pool of VW objects.
        /// </summary>
        protected VowpalWabbitThreadedPredictionBase<TVowpalWabbit> vwPool;

        /// <summary>
        /// True if development mode enabled (additional logging).
        /// </summary>
        protected bool developmentMode;

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        /// <param name="developmentMode">True if development mode enabled (additional logging).</param>
        /// <param name="typeInspector">Type inspector used to extract schema information.</param>
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

        /// <summary>
        /// Determines the action to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <returns>A decision tuple containing the index of the action to take (1-based), and the Id of the model or policy used to make the decision.
        /// Can be null if the Policy is not ready yet (e.g. model not loaded).</returns>
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

        /// <summary>
        /// Sub classes must override and create a new VW pool.
        /// </summary>
        protected abstract VowpalWabbitThreadedPredictionBase<TVowpalWabbit> CreatePool(VowpalWabbitSettings settings);

        /// <summary>
        /// Determines the action to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="vw">The Vowpal Wabbit instance to use.</param>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <returns>A decision tuple containing the index of the action to take (1-based), and the Id of the model or policy used to make the decision.
        /// Can be null if the Policy is not ready yet (e.g. model not loaded).</returns>
        protected abstract PolicyDecision<TAction> MapContext(TVowpalWabbit vw, TContext context);
    }
}
