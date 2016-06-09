using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VW;

namespace ClientDecisionServiceSample
{
    public static class SampleCodeUsingJsonDirectContextClass
    {
        /***** Copy & Paste your authorization token here *****/
        static readonly string SettingsBlobUri = "";

        public static void SampleCodeUsingJsonDirectContext()
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration(settingsBlobUri: SettingsBlobUri);

            using (var service = DecisionService
                .Create<FoodContext>(serviceConfig)
                .ExploitUntilModelReady(new FoodPolicy()))
            {
                System.Threading.Thread.Sleep(10000);

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

                    int[] action = service.ChooseAction(key, currentContext);

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
                Console.WriteLine("Percent correct:" + (((float)counterCorrect) / counterTotal).ToString());

                File.WriteAllText("C:\\Users\\lhoang\\downloads\\scriptData.csv", csv.ToString());
                Console.ReadLine();
            }
        }
    }
}
