using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;

namespace ClientDecisionServiceSample
{
    public class PaperSample
    {
        public class MyContext
        {
            // Feature: Age:25
            public int Age { get; set; }

            // Feature: l:New_York
            [JsonProperty("l")]
            public string Location { get; set; }

            // Logged, but not used as feature due to leading underscore
            [JsonProperty("_isMember")]
            public bool IsMember { get; set; }

            // Not logged, not used as feature due to JsonIgnore
            [JsonIgnore]
            public string SessionId { get; set; }
        }

        public class MyHeuristicPolicy : IContextMapper<MyContext, int>
        {
            public PolicyDecision<int> MapContext(MyContext context)
            {
                return 1;
            }
        }

        public static void Run()
        {
            var config = new DecisionServiceConfiguration("... auth token ...");
            using (var client = DecisionService.Create<MyContext>(config)
                .ExploitUntilModelReady(new MyHeuristicPolicy())) // optional
            {
                var id = Guid.NewGuid().ToString();
                var ctx = new MyContext { Age = 25, Location = "New York", IsMember = true, SessionId = "123" };
                var action = client.ChooseAction(id, ctx);
            }
        }
    }
}
