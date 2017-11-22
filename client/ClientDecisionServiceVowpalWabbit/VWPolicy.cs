using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.Diagnostics;
using System.IO;
using VW;
using VW.Serializer;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Vowpal Wabbit based policy.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public class VWPolicy<TContext> 
        : VWBaseContextMapper<VowpalWabbit<TContext>, TContext, int>, IPolicy<TContext>
    {
        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        public VWPolicy(Stream vwModelStream = null, ITypeInspector typeInspector = null, bool developmentMode = false)
            : base(vwModelStream, typeInspector, developmentMode)
        {
        }

        /// <summary>
        /// Sub classes must override and create a new VW pool.
        /// </summary>
        protected override VowpalWabbitThreadedPredictionBase<VowpalWabbit<TContext>> CreatePool(VowpalWabbitSettings settings)
        {
            return new VowpalWabbitThreadedPrediction<TContext>(settings);
        }

        /// <summary>
        /// Determines the action to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="vw">The Vowpal Wabbit instance to use.</param>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <returns>A decision tuple containing the index of the action to take (1-based), and the Id of the model or policy used to make the decision.
        /// Can be null if the Policy is not ready yet (e.g. model not loaded).</returns>
        protected override PolicyDecision<int> MapContext(VowpalWabbit<TContext> vw, TContext context)
        {
            if (this.developmentMode)
            {
                using (var serializer = vw.Serializer.Create(vw.Native))
                {
                    Trace.TraceInformation("Example Context: {0}", serializer.SerializeToString(context));
                }
            }
            var action = (int)vw.Predict(context, VowpalWabbitPredictionType.CostSensitive);
            var state = new VWState { ModelId = vw.Native.ID };

            return PolicyDecision.Create(action, state);
        }
    }
}