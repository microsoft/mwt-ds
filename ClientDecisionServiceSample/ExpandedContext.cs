using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
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
        [Feature]
        [JsonIgnore]
        public bool Dummy { get; set; }

        [JsonIgnore]
        public float[] Features { get; set; }

        [JsonIgnore]
        public int[] Actions { get; set; }

        [JsonProperty(PropertyName="_multi")]
        public ExpandedActionDependentFeatures[] ActionDependentFeatures
        {
            get
            {
                ExpandedActionDependentFeatures[] adfFeatures = this.Actions
                    .Select(i => new ExpandedActionDependentFeatures(this.Features, i * this.Features.Length))
                    .ToArray();

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

        public static int GetNumberOfActionsFromAdfContext(ExpandedContext context)
        {
            return context.Actions.Length;
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

        public IEnumerable<float> ExpandedFeatures
        {
            get
            {
                return Enumerable.Repeat(0f, this.offset)
                    .Concat(this.features);
            }
        }
    }


    class ExpandedPolicy : IRanker<ExpandedContext>
    {
        public Decision<int[]> MapContext(ExpandedContext context)
        {
            return context.Actions.Select((a, i) => i + 1).ToArray();
        }
    }

}
