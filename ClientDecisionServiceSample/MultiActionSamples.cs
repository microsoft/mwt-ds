using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using VW;
using Newtonsoft.Json;
using VW.Labels;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;

namespace ClientDecisionServiceSample
{
    /// <summary>
    /// Sample code for using the Decision Service when the decision type
    /// involves multiple actions.
    /// </summary>
    public class MultiActionSamples
    {
        /***** Copy & Paste your authorization token here *****/
        static readonly string MwtServiceToken = "";

        /***** Copy & Paste your EventHub configurations here *****/
        static readonly string EventHubConnectionString = "";
        static readonly string EventHubInputName = "";

        public static void SampleCodeUsingASAWithJsonContext()
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration(authorizationToken: MwtServiceToken)
            {
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue,
                EventHubConnectionString = MultiActionSamples.EventHubConnectionString,
                EventHubInputName = MultiActionSamples.EventHubInputName,
            };

            var explorer = DecisionService.WithJsonRanker(serviceConfig).WithTopSlotEpsilonGreedy(epsilon: 0.8f);
            using (var service = DecisionService.CreatePolicyMode(explorer))
            {
                string uniqueKey = "json-key-";

                var rg = new Random(uniqueKey.GetHashCode());

                string baseLocation = "Washington-";

                for (int i = 1; i < 20; i++)
                {

                    DateTime timeStamp = DateTime.UtcNow;
                    string key = uniqueKey + Guid.NewGuid().ToString();

                    var context = new FoodContext { Actions = new int[] { 1, 2, 3 }, UserLocation = baseLocation + rg.Next(100) };
                    var contextJson = JsonConvert.SerializeObject(context);

                    int[] action = service.ChooseAction(new UniqueEventID { Key = key, TimeStamp = timeStamp }, contextJson);
                    service.ReportReward(i / 100f, new UniqueEventID { Key = key, TimeStamp = timeStamp });

                    System.Threading.Thread.Sleep(1);
                }
            }
        }
        public static void SampleCodeUsingASAJoinServer()
        {
            // Create configuration for the decision service

            var serviceConfig = new DecisionServiceConfiguration(authorizationToken: MwtServiceToken)
            {
                EventHubConnectionString = MultiActionSamples.EventHubConnectionString,
                EventHubInputName = MultiActionSamples.EventHubInputName,
                FeatureDiscovery = VowpalWabbitFeatureDiscovery.Json
            };

            using (var service = DecisionService
                .WithRanker<FoodContext, FoodFeature>(serviceConfig, context => FoodContext.GetFeaturesFromContext(context))
                .WithTopSlotEpsilonGreedy(epsilon: .8f)
                .ExploitUntilModel(new FoodPolicy()))
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

        public static void SampleCodeUsingEventHubUploader()
        {
            var uploader = new EventUploaderASA("", "");

            Stopwatch sw = new Stopwatch();

            int numEvents = 100;
            var events = new IEvent[numEvents];
            for (int i = 0; i < numEvents; i++)
            {
                events[i] = new Interaction
                {
                    Key = i.ToString(),
                    Value = 1,
                    Context = "context " + i,
                    ExplorerState = new GenericExplorerState { Probability = (float)i / numEvents }
                };
            }
            //await uploader.UploadAsync(events[0]);
            uploader.Upload(events[0]);

            sw.Start();

            //await uploader.UploadAsync(events.ToList());
            uploader.Upload(events.ToList());

            events = new IEvent[numEvents];
            for (int i = 0; i < numEvents; i++)
            {
                events[i] = new Observation
                {
                    Key = i.ToString(),
                    Value = "observation " + i
                };
            }
            //await uploader.UploadAsync(events.ToList());
            uploader.Upload(events.ToList());

            Console.WriteLine(sw.Elapsed);
        }

        public static void SampleCodeUsingJsonDirectContext()
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
                .WithTopSlotEpsilonGreedy(epsilon: .2f)
                .ExploitUntilModel(new FoodPolicy()))
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

                    int[] action = service.ChooseAction(new UniqueEventID { Key = key, TimeStamp = timeStamp }, currentContext);

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
                    service.ReportReward(reward, new UniqueEventID { Key = key, TimeStamp = timeStamp });
                    var newLine = string.Format("{0},{1},{2}", csvLocation, csvAction, "0");
                    csv.AppendLine(newLine);

                    System.Threading.Thread.Sleep(1);

                }
                Console.WriteLine("Percent correct:" + (((float)counterCorrect) / counterTotal).ToString());

                File.WriteAllText("C:\\Users\\lhoang\\downloads\\scriptData.csv", csv.ToString());
                Console.ReadLine();
            }
        }

        public static void TrainNewVWModelWithMultiActionJsonDirectData()
        {
            int numLocations = 2; // user location
            string[] locations = new string[] { "HealthyTown", "LessHealthyTown" };

            int numActions = 3; // food item
            int numExamplesPerActions = 10000;
            var recorder = new FoodRecorder();

            var stringExamplesTrain = new StringBuilder();
            using (var vw = new VowpalWabbit<FoodContext>(
                new VowpalWabbitSettings(
                    "--cb_adf --rank_all --cb_type dr -q ::",
                    featureDiscovery: VowpalWabbitFeatureDiscovery.Json,
                    enableStringExampleGeneration: true,
                    enableStringFloatCompact: true)))
            {
                // Learn 
                var rand = new Random(0);
                for (int iE = 0; iE < numExamplesPerActions * numLocations; iE++)
                {
                    DateTime timeStamp = DateTime.UtcNow;

                    int iL = rand.Next(0, numLocations);

                    var context = new FoodContext { Actions = new int[] { 1, 2, 3 }, UserLocation = locations[iL] };
                    string key = "fooditem " + Guid.NewGuid().ToString();

                    int action = iE % numActions + 1;
                    recorder.Record(null, null, new EpsilonGreedyState { Probability = 1.0f / numActions }, null, new UniqueEventID { Key = key, TimeStamp = timeStamp });

                    float cost = 0;

                    var draw = rand.NextDouble();
                    if (context.UserLocation == "HealthyTown")
                    {
                        // for healthy town, buy burger 1 with probability 0.1, burger 2 with probability 0.15, salad with probability 0.6
                        if ((action == 1 && draw < 0.1) || (action == 2 && draw < 0.15) || (action == 3 && draw < 0.6))
                        {
                            cost = -10;
                        }
                    }
                    else
                    {
                        // for unhealthy town, buy burger 1 with probability 0.4, burger 2 with probability 0.6, salad with probability 0.2
                        if ((action == 1 && draw < 0.4) || (action == 2 && draw < 0.6) || (action == 3 && draw < 0.2))
                        {
                            cost = -10;
                        }
                    }
                    var label = new ContextualBanditLabel
                    {
                        Action = (uint)(action - 1),
                        Cost = cost,
                        Probability = recorder.GetProb(key)
                    };
                    vw.Learn(context, label);

                    stringExamplesTrain.Append(vw.Serializer.Create(vw.Native).SerializeToString(context, label, (int)label.Action));
                    stringExamplesTrain.Append("\r\n");
                }
                // write training data in string format
                File.WriteAllText(@"c:\users\lhoang\downloads\food_train.vw", stringExamplesTrain.ToString());

                // Predict
                var stringExamplesTest = new StringBuilder();
                var stringExamplesPred = new StringBuilder();
                stringExamplesPred.Append(string.Join(",", locations));
                stringExamplesPred.Append("\r\n");

                for (int iE = 0; iE < numExamplesPerActions; iE++)
                {
                    foreach (string location in locations)
                    {
                        DateTime timeStamp = DateTime.UtcNow;

                        var context = new FoodContext { Actions = new int[] { 1, 2, 3 }, UserLocation = location };
                        int[] predicts = vw.Predict(context, VowpalWabbitPredictionType.Multilabel);
                        stringExamplesPred.Append(predicts[0] + 1);

                        if (location == locations[0])
                        {
                            stringExamplesPred.Append(",");
                        }

                        stringExamplesTest.Append(vw.Serializer.Create(vw.Native).SerializeToString(context));
                        stringExamplesTest.Append("\r\n");
                    }
                    stringExamplesPred.Append("\n");
                }
                // write testing data in string format
                File.WriteAllText(@"c:\users\lhoang\downloads\food_test.vw", stringExamplesTest.ToString());
                // write model predictions
                File.WriteAllText(@"c:\users\lhoang\downloads\food_csharp.pred", stringExamplesPred.ToString());
            }
        }

        /// <summary>
        /// Train a contextual bandit with action dependent features VW model using randomly generated data.
        /// </summary>
        /// <param name="numExamples">Number of examples to generate.</param>
        /// <param name="numActions">Number of actions to use to generate action dependent features for each example.</param>
        /// <returns>New VW model file path.</returns>
        public static string TrainNewVWModelWithRandomData(int numExamples, int numActions)
        {
            Random rg = new Random(numExamples + numActions);

            string vwFileName = string.Format("sample_vw_{0}.model", numExamples);
            if (File.Exists(vwFileName))
            {
                return vwFileName;
            }

            string vwArgs = "--cb_adf --rank_all --quiet";

            using (var vw = new VowpalWabbit<ADFContext, ADFFeatures>(vwArgs))
            {
                //Create examples
                for (int ie = 0; ie < numExamples; ie++)
                {
                    // Create features
                    var context = ADFContext.CreateRandom(numActions, rg);
                    if (ie == 0)
                    {
                        context.Shared = new string[] { "s_1", "s_2" };
                    }

                    vw.Learn(
                        context,
                        context.ActionDependentFeatures,
                        context.ActionDependentFeatures.IndexOf(f => f.Label != null),
                        context.ActionDependentFeatures.First(f => f.Label != null).Label);
                }

                vw.Native.SaveModel(vwFileName);
            }
            return vwFileName;
        }

        private static int GetNumberOfActionsFromAdfContext(ADFContext context)
        {
            return context.ActionDependentFeatures.Count;
        }

        private static IReadOnlyCollection<ADFFeatures> GetFeaturesFromContext(ADFContext context)
        {
            return context.ActionDependentFeatures;
        }
    }
}