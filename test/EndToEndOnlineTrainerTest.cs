using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.DecisionServiceTest
{
    [TestClass]
    public class EndToEndTest : ProvisioningBaseTest
    {
        public EndToEndTest()
        {
            this.deleteOnCleanup = false;
        }

        [TestMethod]
        public void E2ERankerStochasticRewards()
        {
            // Create configuration for the decision service
            float initialEpsilon = .5f;

            this.ConfigureDecisionService(trainArguments: "--cb_explore_adf --cb_type dr -q :: --epsilon 0.2", initialExplorationEpsilon: initialEpsilon);

            string settingsBlobUri = this.settingsUrl;

            float percentCorrect = UploadFoodContextData(settingsBlobUri, firstPass: true);
            Assert.IsTrue(percentCorrect < initialEpsilon);

            percentCorrect = UploadFoodContextData(settingsBlobUri, firstPass: false);
            Assert.IsTrue(percentCorrect > .8f);
        }

        private float UploadFoodContextData(string settingsBlobUri, bool firstPass)
        {
            var serviceConfig = new DecisionServiceConfiguration(settingsBlobUri);

            if (firstPass)
            {
                serviceConfig.PollingForModelPeriod = TimeSpan.MinValue;
                this.OnlineTrainerReset();
            }

            using (var service = DecisionService.Create<FoodContext>(serviceConfig))
            {
                if (!firstPass)
                {
                    Thread.Sleep(10000);
                }

                string uniqueKey = "scratch-key-gal";
                string[] locations = { "HealthyTown", "LessHealthyTown" };

                var rg = new Random(uniqueKey.GetHashCode());

                int numActions = 3; // ["Hamburger deal 1", "Hamburger deal 2" (better), "Salad deal"]

                var csv = new StringBuilder();

                int counterCorrect = 0;
                int counterTotal = 0;

                var header = "Location,Action,Reward";
                csv.AppendLine(header);
                // number of iterations
                for (int i = 0; i < 10000 * locations.Length; i++)
                {
                    // randomly select a location
                    int iL = rg.Next(0, locations.Length);
                    string location = locations[iL];

                    DateTime timeStamp = DateTime.UtcNow;
                    string key = uniqueKey + Guid.NewGuid().ToString();

                    FoodContext currentContext = new FoodContext();
                    currentContext.UserLocation = location;
                    currentContext.Actions = Enumerable.Range(1, numActions).ToArray();

                    int[] action = service.ChooseRanking(key, currentContext);

                    counterTotal += 1;

                    // We expect healthy town to get salad and unhealthy town to get the second burger (action 2)
                    if (location.Equals("HealthyTown") && action[0] == 3)
                        counterCorrect += 1;
                    else if (location.Equals("LessHealthyTown") && action[0] == 2)
                        counterCorrect += 1;

                    var csvLocation = location;
                    var csvAction = action[0].ToString();

                    float reward = 0;
                    double currentRand = rg.NextDouble();
                    if (location.Equals("HealthyTown"))
                    {
                        // for healthy town, buy burger 1 with probability 0.1, burger 2 with probability 0.15, salad with probability 0.6
                        if ((action[0] == 1 && currentRand < 0.1) ||
                            (action[0] == 2 && currentRand < 0.15) ||
                            (action[0] == 3 && currentRand < 0.6))
                        {
                            reward = 10;
                        }
                    }
                    else
                    {
                        // for unhealthy town, buy burger 1 with probability 0.4, burger 2 with probability 0.6, salad with probability 0.2
                        if ((action[0] == 1 && currentRand < 0.4) ||
                            (action[0] == 2 && currentRand < 0.6) ||
                            (action[0] == 3 && currentRand < 0.2))
                        {
                            reward = 10;
                        }
                    }
                    service.ReportReward(reward, key);
                    var newLine = string.Format("{0},{1},{2}", csvLocation, csvAction, "0");
                    csv.AppendLine(newLine);

                    System.Threading.Thread.Sleep(1);

                }
                return (float)counterCorrect / counterTotal;
            }
        }
    }

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
                    .Select((a, i) => new FoodFeature(this.Actions.Length, i))
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
            Scores[index] = index + 1;
        }
    }

    class FoodRecorder : IRecorder<FoodContext, int[]>
    {
        Dictionary<string, float> keyToProb = new Dictionary<string, float>();
        public float GetProb(string key)
        {
            return keyToProb[key];
        }

        public void Record(FoodContext context, int[] value, object explorerState, object mapperState, string uniqueKey)
        {
            keyToProb.Add(uniqueKey, ((EpsilonGreedyState)explorerState).Probability);
        }
    }
}

