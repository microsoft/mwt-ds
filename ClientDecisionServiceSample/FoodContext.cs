using Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using VW.Serializer.Attributes;

namespace ClientDecisionServiceSample
{
    public class FoodContext
    {
        [JsonIgnore]
        public string UserLocation { get; set; }

        [JsonIgnore]
        public int[] Actions { get; set; }

        [JsonProperty(PropertyName = "_multi")]
        public FoodFeature[] ActionDependentFeatures
        {
            get
            {
                FoodFeature[] adfFeatures = this.Actions
                    .Select(i => new FoodFeature { Score = 1 })
                    .ToArray();
                return adfFeatures;
            }
        }

        public static IReadOnlyCollection<FoodFeature> GetFeaturesFromContext(FoodContext context)
        {
            return context.ActionDependentFeatures;
        }
    }

    public class FoodFeature
    {
        public float Score { get; set; }
    }

    class FoodPolicy : IPolicy<FoodContext>
    {
        public PolicyDecisionTuple ChooseAction(FoodContext context, uint numActionsVariable = uint.MaxValue)
        {
            return new PolicyDecisionTuple
            {
                Actions = context.Actions.Select((a, i) => (uint)i + 1).ToArray()
            };
        }
    }
}
