using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExperimentationTest
{
    class TestContext
    {
        public string TestId { get; set; }

        public UserFeatures TestUser { get; set; }

        public SharedItemFeatures TestSharedItem { get; set; }

        [JsonProperty("_multi")]
        public IEnumerable<ItemFeatures> TestItems { get; set; }

        public static TestContext CreateRandom(Random rand, int userId)
        {
            return new TestContext
            {
                TestId = Guid.NewGuid().ToString(),
                TestUser = new UserFeatures { User1 = "User " + userId },
                TestSharedItem = new SharedItemFeatures { Shared1 = $"shared 1 {rand.Next(1000) }", Shared2 = rand.Next(1000) },
                TestItems = new int[rand.Next(5,15)].Select(_ => new ItemFeatures
                {
                    Item1 = (float)rand.NextDouble(),
                    Item2 = new SubItemFeatures
                    {
                        SubItem1 = $"sub item 1 {rand.Next(1000)}",
                        SubItem2 = $"sub item 2 {rand.Next(1000)}"
                    }
                })
            };
        }
    }

    class SharedItemFeatures
    {
        public string Shared1 { get; set; }

        public int Shared2 { get; set; }
    }

    class UserFeatures
    {
        public string User1 { get; set; }
    }

    class ItemFeatures
    {
        public float Item1 { get; set; }

        public SubItemFeatures Item2 { get; set; }
    }

    class SubItemFeatures
    {
        public string SubItem1 { get; set; }

        public string SubItem2 { get; set; }
    }
}
