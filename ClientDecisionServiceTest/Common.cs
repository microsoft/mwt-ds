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
            return 5;
        }
    }
    public class EventBatch
    {
        [JsonProperty(PropertyName = "e")]
        public IList<string> Events { get; set; }

        [JsonProperty(PropertyName = "d")]
        public long ExperimentalUnitDurationInSeconds { get; set; }
    }
}
