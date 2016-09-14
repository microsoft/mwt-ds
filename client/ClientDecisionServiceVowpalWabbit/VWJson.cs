using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.IO;
using System.Linq;
using VW;
using VW.Serializer;
using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class VWJsonPolicy : 
        VWBaseContextMapper<VowpalWabbit, string, int>, 
        IPolicy<string>
    {
        public VWJsonPolicy(Stream vwModelStream = null)
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

        protected override VowpalWabbitThreadedPredictionBase<VowpalWabbit> CreatePool(VowpalWabbitSettings settings)
        {
            return new VowpalWabbitThreadedPrediction(settings);
        }
    }

    public class VWJsonRanker : 
        VWBaseContextMapper<VowpalWabbit, string, int[]>, 
        IRanker<string>, INumberOfActionsProvider<string>
    {
        public VWJsonRanker(Stream vwModelStream = null)
            : base(vwModelStream)
        {
        }

        protected override VowpalWabbitThreadedPredictionBase<VowpalWabbit> CreatePool(VowpalWabbitSettings settings)
        {
            return new VowpalWabbitThreadedPrediction(settings);
        }

        protected override PolicyDecision<int[]> MapContext(VowpalWabbit vw, string context)
        {
            using (var vwJson = new VowpalWabbitJsonSerializer(vw))
            using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context))
            {
                ActionScore[] vwMultilabelPredictions = vwExample.Predict(VowpalWabbitPredictionType.ActionProbabilities);

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
