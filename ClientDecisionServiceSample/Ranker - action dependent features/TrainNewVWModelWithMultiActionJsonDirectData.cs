using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VW;
using VW.Labels;
using VW.Serializer;

namespace ClientDecisionServiceSample
{
    public static class TrainNewVWModelWithMultiActionJsonDirectDataClass
    {
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
                    typeInspector: JsonTypeInspector.Default,
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
                        Action = (uint)action,
                        Cost = cost,
                        Probability = recorder.GetProb(key)
                    };
                    vw.Learn(context, label, index: (int)label.Action - 1);

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

    }
}
