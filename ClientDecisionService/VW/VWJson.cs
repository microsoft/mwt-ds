using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VW;
using VW.Serializer;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class VWJsonPolicy : 
        VWBaseContextMapper<VowpalWabbitThreadedPrediction, VowpalWabbit, string, int>, 
        IPolicy<string>
    {
        internal VWJsonPolicy(Stream vwModelStream = null)
            : base(vwModelStream)
        {
        }

        protected override Decision<int> MapContext(VowpalWabbit vw, string context)
        {
            using (var vwJson = new VowpalWabbitJsonSerializer(vw))
            using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context))
            {
                var action = (int)vwExample.Predict(VowpalWabbitPredictionType.CostSensitive);
                var state = new VWState { ModelId = vw.ID };

                return Decision.Create(action, state);
            }
        }
    }

    internal class VWJsonRanker : 
        VWBaseContextMapper<VowpalWabbitThreadedPrediction, VowpalWabbit, string, int[]>, 
        IRanker<string>, INumberOfActionsProvider<string>
    {
        internal VWJsonRanker(Stream vwModelStream = null)
            : base(vwModelStream)
        {
        }

        protected override Decision<int[]> MapContext(VowpalWabbit vw, string context)
        {
            using (var vwJson = new VowpalWabbitJsonSerializer(vw))
            using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context))
            {
                int[] vwMultilabelPredictions = vwExample.Predict(VowpalWabbitPredictionType.Multilabel);

                // VW multi-label predictions are 0-based
                var actions = vwMultilabelPredictions.Select(a => a + 1).ToArray();
                var state = new VWState { ModelId = vw.ID };

                return Decision.Create(actions, state);
            }
        }

        public int GetNumberOfActions(string context)
        {
            return VowpalWabbitJsonSerializer.GetNumberOfActionDependentExamples(context);
        }
    }
}
