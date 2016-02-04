using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VW.Interfaces;
using VW.Labels;
using VW.Serializer.Attributes;

namespace ClientDecisionServiceSample
{
    public class ExpandedContext
    {
        [JsonProperty]
        public float[] Features { get; set; }

        [JsonProperty]
        public int[] Actions { get; set; }

        [JsonIgnore]
        public ExpandedActionDependentFeatures[] ActionDependentFeatures
        {
            get
            {
                ExpandedActionDependentFeatures[] adfFeatures = this.Actions
                    .Select(i => new ExpandedActionDependentFeatures(this.Features, i * this.Features.Length))
                    .ToArray();

                adfFeatures[0].Label = new ContextualBanditLabel
                {
                    Cost = 0.5f,
                    Probability = 0.5f
                };

                return adfFeatures;
            }
        }

        public static ExpandedContext CreateRandom(int numActions, Random rg)
        {
            int iCB = rg.Next(0, numActions);
            int numFeatures = rg.Next(5, 20);
            float[] features = new float[numFeatures].Select(f => (float)rg.NextDouble()).ToArray();

            var context = new ExpandedContext
            {
                Features = features,
                Actions = Enumerable.Range(rg.Next(0, 5), numActions).ToArray()
            };
            return context;
        }

        public static uint GetNumberOfActionsFromAdfContext(ExpandedContext context)
        {
            return (uint)context.Actions.Length;
        }

        public static IReadOnlyCollection<ExpandedActionDependentFeatures> GetFeaturesFromContext(ExpandedContext context)
        {
            return context.ActionDependentFeatures;
        }
    }

    public class ExpandedActionDependentFeatures
    {
        private float[] features;

        private int offset;

        internal ExpandedActionDependentFeatures(float[] features, int offset)
        {
            this.features = features;
            this.offset = offset;
        }

        [Feature]
        public IEnumerable<float> ExpandedFeatures
        {
            get
            {
                return Enumerable.Repeat(0f, this.offset)
                    .Concat(this.features);
            }
        }

        public ILabel Label { get; set; }
    }


    class ExpandedPolicy : IPolicy<ExpandedContext>
    {
        public uint[] ChooseAction(ExpandedContext context, uint numActionsVariable = uint.MaxValue)
        {
            return context.Actions.Select((a, i) => (uint)i + 1).ToArray();
        }
    }

}
