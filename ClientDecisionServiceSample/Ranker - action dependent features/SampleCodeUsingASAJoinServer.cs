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
        static readonly string SettingsBlobUri = "";

        public static void SampleCodeUsingASAJoinServer()
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration(settingsBlobUri: SettingsBlobUri);

            using (var service = DecisionService
                .WithRanker(serviceConfig)
                .With<FoodContext, FoodFeature>(context => FoodContext.GetFeaturesFromContext(context))
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
                    int[] action = service.ChooseAction(key, context);
                    service.ReportReward(i / 100f, key);

                    System.Threading.Thread.Sleep(1);
                }
            }
        }
    }
}
