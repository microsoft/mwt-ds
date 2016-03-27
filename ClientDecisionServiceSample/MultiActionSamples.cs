using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction;
using System.IO;
using VW;
using Newtonsoft.Json;
using VW.Labels;

namespace ClientDecisionServiceSample
{
    /// <summary>
    /// Sample code for using the Decision Service when the decision type
    /// involves multiple actions.
    /// </summary>
    public class MultiActionSamples
    {
        /***** Copy & Paste your auth token here *****/
        static readonly string AuthorizationToken = "f3adf463-4552-4d19-98c5-3b185d7336b7";

        /***** Copy & Paste your EventHub configurations here *****/
        static readonly string EventHubConnectionString = "Endpoint=sb://lhtest73serviceBus.servicebus.windows.net/;SharedAccessKeyName=SendOnlyKey;SharedAccessKey=DG4TcCs+XnDN83VZcT2e5NfVeSFvTuiYUiWtXhxnJ2s=";
        static readonly string EventHubInputName = "lhtest73ingress";

        public static void SampleCodeUsingASAWithJsonContext()
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceJsonConfiguration( // specify that context types are Json-formatted
                authorizationToken: AuthorizationToken,
                explorer: new EpsilonGreedyExplorer<string>(new DefaultJsonPolicy(), epsilon: 0.8f))
            {
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue,
                EventHubConnectionString = MultiActionSamples.EventHubConnectionString,
                EventHubInputName = MultiActionSamples.EventHubInputName,
            };

            using (var service = new DecisionServiceJson(serviceConfig))
            {
                string uniqueKey = "json-key-";

                var rg = new Random(uniqueKey.GetHashCode());

                int numActions = 3;
                string baseLocation = "Washington-";

                for (int i = 1; i < 20; i++)
                {

                    DateTime timeStamp = DateTime.UtcNow;
                    string key = uniqueKey + Guid.NewGuid().ToString();

                    var context = new FoodContext { Actions = new int[] { 1, 2, 3 }, UserLocation = baseLocation + rg.Next(100) };
                    var contextJson = JsonConvert.SerializeObject(context);

                    uint[] action = service.ChooseAction(new UniqueEventID { Key = key, TimeStamp = timeStamp }, contextJson, (uint)numActions);
                    service.ReportReward(i / 100f, new UniqueEventID { Key = key, TimeStamp = timeStamp });

                    System.Threading.Thread.Sleep(1);
                }
            }
        }
        public static void SampleCodeUsingASAJoinServer()
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration<FoodContext, FoodFeature>(
                authorizationToken: AuthorizationToken,
                explorer: new EpsilonGreedyExplorer<FoodContext>(new FoodPolicy(), epsilon: 0.8f))
            {
                EventHubConnectionString = MultiActionSamples.EventHubConnectionString,
                EventHubInputName = MultiActionSamples.EventHubInputName,
                GetContextFeaturesFunc = FoodContext.GetFeaturesFromContext,
                FeatureDiscovery = VowpalWabbitFeatureDiscovery.Json
            };

            using (var service = new DecisionService<FoodContext, FoodFeature>(serviceConfig))
            {
                //string uniqueKey = "sample-asa-client-";
                string uniqueKey = "scratch-key-";
                string baseLocation = "Washington-";
                int numActions = 3;

                var rg = new Random(uniqueKey.GetHashCode());

                for (int i = 1; i < 20; i++)
                {
                    DateTime timeStamp = DateTime.UtcNow;
                    string key = uniqueKey + Guid.NewGuid().ToString();

                    var context = new FoodContext { Actions = new int[] { 1, 2, 3 }, UserLocation = baseLocation + rg.Next(100) };
                    uint[] action = service.ChooseAction(new UniqueEventID { Key = key, TimeStamp = timeStamp }, context, (uint)numActions);
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
                events[i] = new SingleActionInteraction
                {
                    Key = i.ToString(),
                    Action = 1,
                    Context = "context " + i,
                    Probability = (float)i / numEvents
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
            var serviceConfig = new DecisionServiceConfiguration<FoodContext, FoodFeature>(
                authorizationToken: AuthorizationToken,
                explorer: new EpsilonGreedyExplorer<FoodContext>(new FoodPolicy(), epsilon: 0.2f))
            {
                EventHubConnectionString = EventHubConnectionString,
                EventHubInputName = EventHubInputName,
                GetContextFeaturesFunc = FoodContext.GetFeaturesFromContext,
                FeatureDiscovery = VowpalWabbitFeatureDiscovery.Json
            };

            using (var service = new DecisionService<FoodContext, FoodFeature>(serviceConfig))
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

                    uint[] action = service.ChooseAction(new UniqueEventID { Key = key, TimeStamp = timeStamp }, currentContext, (uint)numActions);

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
                        if ((action[0] == 1 && currentRand < 0.1)  || 
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

        public static void SampleCodeUsingActionDependentFeatures()
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration<ADFContext, ADFFeatures>(
                authorizationToken: "",
                explorer: new EpsilonGreedyExplorer<ADFContext>(new ADFPolicy(), epsilon: 0.8f))
            {
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue
            };

            using (var service = new DecisionService<ADFContext, ADFFeatures>(serviceConfig))
            {
                string uniqueKey = "eventid";

                var rg = new Random(uniqueKey.GetHashCode());

                var vwPolicy = new VWPolicy<ADFContext, ADFFeatures>(GetFeaturesFromContext);

                for (int i = 1; i < 100; i++)
                {
                    if (i == 30)
                    {
                        string vwModelFile = TrainNewVWModelWithRandomData(numExamples: 5, numActions: 10);

                        vwPolicy = new VWPolicy<ADFContext, ADFFeatures>(GetFeaturesFromContext, vwModelFile);

                        // Alternatively, VWPolicy can also be loaded from an IO stream:
                        // var vwModelStream = new MemoryStream(File.ReadAllBytes(vwModelFile));
                        // vwPolicy = new VWPolicy<ADFContext, ADFFeatures>(GetFeaturesFromContext, vwModelStream);

                        // Manually updates decision service with a new policy for consuming VW models.
                        service.UpdatePolicy(vwPolicy);
                    }
                    if (i == 60)
                    {
                        string vwModelFile = TrainNewVWModelWithRandomData(numExamples: 6, numActions: 8);

                        // Evolves the existing VWPolicy with a new model
                        vwPolicy.ModelUpdate(vwModelFile);
                    }

                    int numActions = rg.Next(5, 10);
                    uint[] action = service.ChooseAction(new UniqueEventID { Key = uniqueKey }, ADFContext.CreateRandom(numActions, rg), (uint)numActions);
                    service.ReportReward(i / 100f, new UniqueEventID { Key = uniqueKey });
                }
            }
        }

        public static void TrainNewVWModelWithMultiActionJsonDirectData()
        {
            int numLocations = 2; // user location
            string[] locations = new string[] { "HealthyTown", "LessHealthyTown" };

            int numActions = 3; // food item
            int numExamplesPerActions = 10000;
            var recorder = new FoodRecorder();

            //var serviceConfig = new DecisionServiceConfiguration<FoodContext, FoodFeature>(
            //    authorizationToken: AuthorizationToken,
            //    explorer: new EpsilonGreedyExplorer<FoodContext>(new FoodPolicy(), epsilon: 0.2f))
            //{
            //    OfflineMode = true,
            //    Recorder = recorder,
            //    GetContextFeaturesFunc = FoodContext.GetFeaturesFromContext,
            //    FeatureDiscovery = VowpalWabbitFeatureDiscovery.Json
            //};

            var stringExamplesTrain = new StringBuilder();
            //using (var service = new DecisionService<FoodContext, FoodFeature>(serviceConfig))
            //using (var vw = new VowpalWabbit(new VowpalWabbitSettings("--cb_adf --rank_all --cb_type dr")))
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

                    //uint[] chosenActions = service.ChooseAction(new UniqueEventID { Key = key, TimeStamp = timeStamp}, context, (uint)numActions);
                    //uint action = chosenActions[0];
                    uint action = (uint)(iE % numActions + 1);
                    recorder.Record(null, null, 1.0f / numActions, new UniqueEventID { Key = key, TimeStamp = timeStamp });

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
                        Action = action - 1,
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

        private static uint GetNumberOfActionsFromAdfContext(ADFContext context)
        {
            return (uint)context.ActionDependentFeatures.Count;
        }

        private static IReadOnlyCollection<ADFFeatures> GetFeaturesFromContext(ADFContext context)
        {
            return context.ActionDependentFeatures;
        }
    }
}
