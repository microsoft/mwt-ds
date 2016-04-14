using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.IO;
using VW;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class VWPolicy<TContext> 
        : VWBaseContextMapper<VowpalWabbitThreadedPrediction<TContext>, VowpalWabbit<TContext>, TContext, int>, IPolicy<TContext>
    {
        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        internal VWPolicy(Stream vwModelStream = null, VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json)
            : base(vwModelStream, featureDiscovery)
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