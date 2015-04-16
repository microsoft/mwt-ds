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

    public class EventBatch
    {
        [JsonProperty(PropertyName = "e")]
        public IList<string> Events { get; set; }

        [JsonProperty(PropertyName = "d")]
        public long ExperimentalUnitDurationInSeconds { get; set; }
    }

    public static class Constants
    {
        public static readonly uint NumberOfActions = 5;
    }
}
