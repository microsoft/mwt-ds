using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
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
        public string UserLocation { get; set; }

        [JsonIgnore]
        public int[] Actions { get; set; }

        [JsonProperty(PropertyName = "_multi")]
        public FoodFeature[] ActionDependentFeatures
        {
            get
            {
                return this.Actions
                    .Select( (a, i) => new FoodFeature(this.Actions.Length, i))
                    .ToArray();
            }
        }

        public static IReadOnlyCollection<FoodFeature> GetFeaturesFromContext(FoodContext context)
        {
            return context.ActionDependentFeatures;
        }
    }

    public class FoodFeature
    {
        public float[] Scores { get; set; }

        internal FoodFeature(int numActions, int index)
        {
            Scores = Enumerable.Repeat(0f, numActions).ToArray();
            Scores[index] = 1;
        }
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

    class FoodRecorder : IRecorder<FoodContext>
    {
        Dictionary<string, float> keyToProb = new Dictionary<string, float>();
        public void Record(FoodContext context, uint[] actions, float probability, UniqueEventID uniqueKey, string modelId = null, bool? isExplore = null)
        {
            keyToProb.Add(uniqueKey.Key, probability);
        }
        public float GetProb(string key)
        {
            return keyToProb[key];
        }
    }

    //public class FoodContext
    //{
    //    public string UserLocation { get; set; }

    //    [JsonIgnore]
    //    public int[] Actions { get; set; }

    //    [JsonProperty(PropertyName = "_multi")]
    //    public FoodFeature[] ActionDependentFeatures
    //    {
    //        get
    //        {
    //            FoodFeature[] adfFeatures = this.Actions
    //                .Select(i => new FoodFeature(score: 1, offset: i))
    //                .ToArray();
    //            return adfFeatures;
    //        }
    //    }

    //    public static IReadOnlyCollection<FoodFeature> GetFeaturesFromContext(FoodContext context)
    //    {
    //        return context.ActionDependentFeatures;
    //    }
    //}

    //public class FoodFeature
    //{
    //    private float score;
    //    private int offset;

    //    internal FoodFeature(float score, int offset)
    //    {
    //        this.score = score;
    //        this.offset = offset;
    //    }

    //    public IEnumerable<float> ExpandedFeatures
    //    {
    //        get
    //        {
    //            return Enumerable.Repeat(0f, this.offset)
    //                .Concat(new float[] { this.score });
    //        }
    //    }
    //}
}
