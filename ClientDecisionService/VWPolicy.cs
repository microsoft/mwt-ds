using MultiWorldTesting.SingleAction;
using System;
using System.Diagnostics;
using System.IO;
using VW;

namespace ClientDecisionService
{
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
        public VWPolicy(string vwModelFile = null)
        {
            if (vwModelFile == null)
            {
                this.vwPool = new ObjectPool<VowpalWabbit<TContext>>(null);
            }
            else
            {
                this.ModelUpdate(vwModelFile);
            }
        }

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        public VWPolicy(Stream vwModelStream)
        {
            this.ModelUpdate(vwModelStream);
        }

        /// <summary>
        /// Scores the model against the specified context and returns the chosen action (1-based index).
        /// </summary>
        /// <param name="context">The context object.</param>
        /// <returns>An unsigned integer representing the chosen action.</returns>
        public uint ChooseAction(TContext context)
        {
            using (var vw = vwPool.Get())
            using (IVowpalWabbitExample example = vw.Value.ReadExample(context))
            {
                return (uint)example.Predict<VowpalWabbitCostSensitivePrediction>().Value;
            }
        }

        /// <summary>
        /// Update VW model from file.
        /// </summary>
        /// <param name="modelFile">The model file to load.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(string modelFile)
        {
            return ModelUpdate(() => { return new VowpalWabbitModel(string.Format("--quiet -t -i {0}", modelFile)); });
        }

        /// <summary>
        /// Update VW model from stream.
        /// </summary>
        /// <param name="modelStream">The model stream to load from.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Stream modelStream)
        {
            return ModelUpdate(() => { return new VowpalWabbitModel("--quiet -t", modelStream); });
        }

        /// <summary>
        /// Update VW model using a generic method which loads the model.
        /// </summary>
        /// <param name="loadModelFunc">The generic method to load the model.</param>
        /// <returns>true if the update was successful; otherwise, false.</returns>
        public bool ModelUpdate(Func<VowpalWabbitModel> loadModelFunc)
        {
            VowpalWabbitModel vwModel = null;
            try
            {
                vwModel = loadModelFunc();
            }
            catch (Exception ex)
            {
                Trace.TraceError("Unable to initialize VW.");
                Trace.TraceError(ex.ToString());

                return false;
            }

            var factory = new VowpalWabbitFactory<TContext>(vwModel, new VW.Serializer.VowpalWabbitSerializerSettings { MaxExampleCacheSize = 1024 });

            if (this.vwPool == null)
            {
                this.vwPool = new ObjectPool<VowpalWabbit<TContext>>(factory);
            }
            else
            {
                vwPool.UpdateFactory(factory);
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

        private ObjectPool<VowpalWabbit<TContext>> vwPool;
    }
}