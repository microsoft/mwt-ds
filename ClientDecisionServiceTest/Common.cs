using ClientDecisionService;
using MultiWorldTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ClientDecisionServiceTest
{
    class TestContext { }

    class TestOutcome { }

    class TestPolicy : IPolicy<TestContext>
    {
        public uint ChooseAction(TestContext context)
        {
            // Always returns the same action regardless of context
            return Constants.NumberOfActions - 1;
        }
    }

    class TestLogger : ILogger<TestContext>
    {
        public TestLogger()
        {
            this.numRecord = 0;
            this.numReward = 0;
            this.numOutcome = 0;
        }

        public void ReportReward(float reward, string uniqueKey)
        {
            this.numReward++;
        }

        public void ReportOutcome(string outcomeJson, string uniqueKey)
        {
            this.numOutcome++;
        }

        public void Flush()
        {
            this.numRecord = 0;
            this.numReward = 0;
            this.numOutcome = 0;
        }

        public void Record(TestContext context, uint action, float probability, string uniqueKey)
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
        public List<CompleteExperimentalUnitFragment> Fragments { get; set; }
    }

    public class CompleteExperimentalUnitFragment
    {
        [JsonProperty(PropertyName = "t", Required = Required.Always)]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "a")]
        public int? Action { get; set; }

        [JsonProperty(PropertyName = "p")]
        public float? Probability { get; set; }

        [JsonProperty(PropertyName = "c")]
        [JsonConverter(typeof(RawStringConverter))]
        public object Context { get; set; }

        [JsonProperty(PropertyName = "v")]
        public object Value { get; set; }
    }

    public static class Constants
    {
        public static readonly uint NumberOfActions = 5;
    }
}
