using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VW;

namespace ClientDecisionServiceSample
{
    public static class SampleCodeUsingASAJoinServerClass
    {
        /***** Copy & Paste your authorization token here *****/
        static readonly string MwtServiceToken = "";

        /***** Copy & Paste your EventHub configurations here *****/
        static readonly string EventHubConnectionString = "";
        static readonly string EventHubInputName = "";

        public static void SampleCodeUsingASAJoinServer()
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration(authorizationToken: MwtServiceToken)
            {
                EventHubConnectionString = EventHubConnectionString,
                EventHubInputName = EventHubInputName,
                FeatureDiscovery = VowpalWabbitFeatureDiscovery.Json
            };

            using (var service = DecisionService
                .WithRanker<FoodContext, FoodFeature>(serviceConfig, context => FoodContext.GetFeaturesFromContext(context))
                .WithTopSlotEpsilonGreedy(epsilon: .8f)
                .ExploitUntilModelReady(new FoodPolicy()))
            {
                string uniqueKey = "scratch-key-";
                string baseLocation = "Washington-";

                var rg = new Random(uniqueKey.GetHashCode());

                for (int i = 1; i < 20; i++)
                {
                    DateTime timeStamp = DateTime.UtcNow;
                    string key = uniqueKey + Guid.NewGuid().ToString();

                    var context = new FoodContext { Actions = new int[] { 1, 2, 3 }, UserLocation = baseLocation + rg.Next(100) };
                    int[] action = service.ChooseAction(new UniqueEventID { Key = key, TimeStamp = timeStamp }, context);
                    service.ReportReward(i / 100f, new UniqueEventID { Key = key, TimeStamp = timeStamp });

                    System.Threading.Thread.Sleep(1);
                }
            }
        }
    }
}
