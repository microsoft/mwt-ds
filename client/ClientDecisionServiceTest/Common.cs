using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VW.Labels;
using VW.Serializer.Attributes;

namespace ClientDecisionServiceTest
{
    enum InterfaceType
    { 
        SingleAction,
        MultiAction
    }

    public class TestContext
    {
        public int A { get; set; }

        [JsonProperty(PropertyName = "_multi")]
        public IReadOnlyList<TestContextFeatures> ActionDependentFeatures
        {
            get 
            {
                return new int[5].Select(_ => new TestContextFeatures()).ToArray();
            }
        }
    }

    public class TestContextFeatures
    {
        public string[] Features { get { return new string[] { "aa", "bb" }; } }
    }

    class DummyADFType { }

    public class TestADFContext
    {
        public TestADFContext(int count)
        {
            this.count = count;
        }

        [JsonProperty(PropertyName = "_multi")]
        public IReadOnlyList<string> ActionDependentFeatures
        {
            get
            {
                var features = new string[count];
                for (int i = 0; i < count; i++)
                {
                    features[i] = i.ToString();
                }

                return features;
            }
        }

        private int count;
    }

    public class TestADFContextWithFeatures
    {
        public string[] Shared { get; set; }

        [JsonProperty("_multi")]
        public IReadOnlyList<TestADFFeatures> ActionDependentFeatures { get; set; }

        public static TestADFContextWithFeatures CreateRandom(int numActions, Random rg)
        {
            int iCB = rg.Next(0, numActions);

            var fv = new TestADFFeatures[numActions];
            for (int i = 0; i < numActions; i++)
            {
                fv[i] = new TestADFFeatures
                {
                    Features = new[] { "a_" + (i + 1), "b_" + (i + 1), "c_" + (i + 1) }
                };

                if (i == iCB) // Randomly place a Contextual Bandit label
                {
                    fv[i].Label = new ContextualBanditLabel
                    {
                        Cost = (float)rg.NextDouble(),
                        Probability = (float)rg.NextDouble()
                    };
                }
            }

            var context = new TestADFContextWithFeatures
            {
                Shared = new string[] { "shared", "features" },
                ActionDependentFeatures = fv
            };
            return context;
        }
    }

    public class TestADFFeatures
    {
        [Feature]
        public string[] Features { get; set; }

        public override string ToString()
        {
            return string.Join(" ", this.Features);
        }

        public ILabel Label { get; set; }
    }

    public class TestRcv1Context
    {
        [Feature(FeatureGroup = 'f', Namespace = "eatures", Order = 1)]
        public IList<KeyValuePair<string, float>> Features { get; set; }

        public ILabel Label { get; set; }

        public static TestRcv1Context CreateRandom(int numActions, int numFeatures, Random rand)
        {
            var features = new List<KeyValuePair<string, float>>();
            for (int i = 0; i < numFeatures; i++)
            {
                features.Add(new KeyValuePair<string, float>(i.ToString(), (float)rand.NextDouble()));
            }
            return new TestRcv1Context
            {
                Features = features,
                Label = new ContextualBanditLabel
                {
                    Action = (uint)(rand.Next(0, numActions) + 1),
                    Cost = (float)rand.NextDouble(),
                    Probability = (float)rand.NextDouble()
                }
            };
        }
    }

    class TestOutcome { }

    public class ConstantPolicy<T> : IContextMapper<T, ActionProbability[]>
    {
        private Func<T, int> numActionsFunc;

        public ConstantPolicy(Func<T, int> numActionsFunc = null)
        {
            if (numActionsFunc != null)
                this.numActionsFunc = numActionsFunc ;
            else
                this.numActionsFunc = _ => Constants.NumberOfActions - 1;
        }

        public Task<PolicyDecision<ActionProbability[]>> MapContextAsync(T context)
        {
            return Task.FromResult(PolicyDecision.Create(Enumerable.Range(1, this.numActionsFunc(context))
                .Select(a => new ActionProbability { Action = a, Probability = a == 1 ? 1f : 0 })
                .ToArray()));
        }
    }

    class TestLogger : ILogger, IRecorder<TestContext, int>, IRecorder<TestContext, int[]>, IDisposable
    {
        public TestLogger()
        {
            this.numRecord = 0;
            this.numReward = 0;
            this.numOutcome = 0;
        }

        public void ReportReward(string uniqueKey, float reward)
        {
            this.numReward++;
        }

        public void ReportOutcome(string uniqueKey, object outcome)
        {
            this.numOutcome++;
        }

        public void Dispose()
        {
            this.numRecord = 0;
            this.numReward = 0;
            this.numOutcome = 0;
        }

        public void Record(TestContext context, int value, object explorerState, object mapperState, string uniqueKey)
        {
            this.numRecord++;
        }

        public void Record(TestContext context, int[] value, object explorerState, object mapperState, string uniqueKey)
        {
            this.numRecord++;
        }

        public int NumRecord
        {
            get { return numRecord; }
        }

        public int NumReward
        {
            get { return numReward; }
        }

        public int NumOutcome
        {
            get { return numOutcome; }
        }

        int numRecord;
        int numReward;
        int numOutcome;
    }

    public class PartialExperimentalUnitFragment
    {
        [JsonProperty(PropertyName = "i")]
        public string Id { get; set; }

        // TODO: does shortening really makes sense? http://stackoverflow.com/questions/22880344/shorten-property-names-in-json-does-it-make-sense
        [JsonProperty(PropertyName = "v")]
        [JsonConverter(typeof(RawStringConverter))]
        public string Value { get; set; }
    }

    public class PartialDecisionServiceMessage
    {
        [JsonProperty(PropertyName = "i", Required = Required.Always)]
        public Guid ID { get; set; }

        [JsonProperty(PropertyName = "j", Required = Required.Always)]
        public List<PartialExperimentalUnitFragment> ExperimentalUnitFragments { get; set; }

        [JsonProperty(PropertyName = "d")]
        public int? ExperimentalUnitDuration { get; set; }
    }

    public class CompleteDecisionServiceBlob
    {
        [JsonProperty(PropertyName = "blob", Required = Required.Always)]
        public Guid Blob { get; set; }

        [JsonProperty(PropertyName = "data", Required = Required.Always)]
        public List<CompleteExperimentalUnitData> Data { get; set; }
    }

    public class CompleteExperimentalUnitData
    {
        [JsonProperty(PropertyName = "i", Required = Required.Always)]
        public string Key { get; set; }

        [JsonProperty(PropertyName = "f", Required = Required.Always)]
        public List<SingleActionCompleteExperimentalUnitFragment> Fragments { get; set; }
    }

    public class BaseCompleteExperimentalUnitFragment
    {
        [JsonProperty(PropertyName = "p")]
        public float[] Probabilities { get; set; }

        [JsonProperty(PropertyName = "c")]
        [JsonConverter(typeof(RawStringConverter))]
        public object Context { get; set; }

        [JsonProperty(PropertyName = "v")]
        [JsonConverter(typeof(RawStringConverter))]
        public object Value { get; set; }
    }

    public class SingleActionCompleteExperimentalUnitFragment : BaseCompleteExperimentalUnitFragment
    {
        [JsonProperty(PropertyName = "a")]
        public int? Action { get; set; }
    }

    public class MultiActionCompleteExperimentalUnitFragment : BaseCompleteExperimentalUnitFragment
    {
        [JsonProperty(PropertyName = "a")]
        public int[] Actions { get; set; }
    }

    public static class Constants
    {
        public static readonly int NumberOfActions = 5;
    }
}
