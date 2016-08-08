using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.IO;
using System.Linq;
using VW;
using VW.Serializer;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class VWJsonPolicy : 
        VWBaseContextMapper<VowpalWabbitThreadedPrediction, VowpalWabbit, string, int>, 
        IPolicy<string>
    {
        internal VWJsonPolicy(Stream vwModelStream = null)
            : base(vwModelStream)
        {
        }

        protected override PolicyDecision<int> MapContext(VowpalWabbit vw, string context)
        {
            using (var vwJson = new VowpalWabbitJsonSerializer(vw))
            using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context))
            {
                var action = (int)vwExample.Predict(VowpalWabbitPredictionType.CostSensitive);
                var state = new VWState { ModelId = vw.ID };

                return PolicyDecision.Create(action, state);
            }
        }
    }

    public class VWJsonRanker : 
        VWBaseContextMapper<VowpalWabbitThreadedPrediction, VowpalWabbit, string, int[]>, 
        IRanker<string>, INumberOfActionsProvider<string>
    {
        internal VWJsonRanker(Stream vwModelStream = null)
            : base(vwModelStream)
        {
        }

        protected override PolicyDecision<int[]> MapContext(VowpalWabbit vw, string context)
        {
            using (var vwJson = new VowpalWabbitJsonSerializer(vw))
            using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context))
            {
                ActionScore[] vwMultilabelPredictions = vwExample.Predict(VowpalWabbitPredictionType.ActionScore);

                // VW multi-label predictions are 0-based
                var actions = vwMultilabelPredictions.Select(a => (int)a.Action + 1).ToArray();
                var state = new VWState { ModelId = vw.ID };

                return PolicyDecision.Create(actions, state);
            }
        }

        public int GetNumberOfActions(string context)
        {
            return VowpalWabbitJsonSerializer.GetNumberOfActionDependentExamples(context);
        }
    }
}
