using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceSample
{
    public static class SampleCodeUsingASAWithJsonContextClass
    {
        /***** Copy & Paste your authorization token here *****/
        static readonly string SettingsBlobUri = "";

        public static void SampleCodeUsingASAWithJsonContext()
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration(settingsBlobUri: SettingsBlobUri)
            {
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue
            };

            using (var service = DecisionService.CreateJson(serviceConfig))
            {
                string uniqueKey = "json-key-";

                var rg = new Random(uniqueKey.GetHashCode());

                string baseLocation = "Washington-";

                for (int i = 1; i < 20; i++)
                {
                    DateTime timeStamp = DateTime.UtcNow;
                    string key = uniqueKey + Guid.NewGuid().ToString();

                    var context = new FoodContext { Actions = new int[] { 1, 2, 3 }, UserLocation = baseLocation + rg.Next(100) };
                    // TODO: louie: I guess we're using JsonConvert here for making our life easier, but this can be confusing to sample readers
                    // if 
                    var contextJson = JsonConvert.SerializeObject(context);

                    int[] action = service.ChooseRanking(key, contextJson);
                    service.ReportReward(i / 100f, key);

                    System.Threading.Thread.Sleep(1);
                }
            }
        }
    }
}
