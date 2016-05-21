using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.IO;
using VW;
using VW.Serializer;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class VWPolicy<TContext> 
        : VWBaseContextMapper<VowpalWabbitThreadedPrediction<TContext>, VowpalWabbit<TContext>, TContext, int>, IPolicy<TContext>
    {
        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        internal VWPolicy(Stream vwModelStream = null, ITypeInspector typeInspector = null)
            : base(vwModelStream, typeInspector)
        {
        }

        protected override PolicyDecision<int> MapContext(VowpalWabbit<TContext> vw, TContext context)
        {
            var action = (int)vw.Predict(context, VowpalWabbitPredictionType.CostSensitive);
            var state = new VWState { ModelId = vw.Native.ID };

            return PolicyDecision.Create(action, state);
        }
    }
}