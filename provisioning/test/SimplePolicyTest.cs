using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.DecisionServiceTest
{
    [TestClass]
    public class SimplePolicyTestClass : ProvisioningBaseTest
    {
        private const string deploymentOutput = @"
{
  ""management Center URL"": {
    ""type"": ""String"",
    ""value"": ""https://mcunitc2fe50084aeb9158320f9ff760a7-mc-ftupw3x6ndhxq.azurewebsites.net""
  },
  ""management Center Password"": {
    ""type"": ""String"",
    ""value"": ""mv6t3nnz5uq7w""
  },
  ""client Library URL"": {
    ""type"": ""String"",
    ""value"": ""https://storageftupw3x6ndhxq.blob.core.windows.net/mwt-settings/client?sv=2015-07-08&sr=b&sig=BOgAw8%2Fxk7h7Rq5Qep2k1REmcLy0KNyU8ZbMaI%2F6FQI%3D&st=2016-06-20T17%3A16%3A39Z&se=2017-06-20T17%3A17%3A39Z&sp=r""
  },
  ""web Service Token"": {
    ""type"": ""String"",
    ""value"": ""bd6v3grappmxo""
  },
  ""online Trainer Token"": {
    ""type"": ""String"",
    ""value"": ""t6ymqbvdphtvs""
  },
  ""online Trainer URL"": {
    ""type"": ""String"",
    ""value"": ""http://mcunitc2fe50084aeb9158320f9ff760a7-trainer-ftupw3x6ndhxq.cloudapp.net""
  }
}

";

        private Dictionary<string, int> freq;
        private string[] features;
        private Random rnd;
        private int eventCount;

        //public SimplePolicyTestClass() : base(deploymentOutput) { }

        [TestMethod]
        [TestCategory("End to End")]
        [Priority(2)]
        public async Task SimplePolicyTest()
        {
            this.ConfigureDecisionService("--cb_explore 4 --epsilon 0", initialExplorationEpsilon:1, isExplorationEnabled: true);

            // 4 Actions
            // why does this need to be different from default?
            var config = new DecisionServiceConfiguration(settingsUrl)
            {
                InteractionUploadConfiguration = new BatchingConfiguration
                {
                    MaxEventCount = 64
                },
                ObservationUploadConfiguration = new BatchingConfiguration
                {
                    MaxEventCount = 64
                }
            };

            config.InteractionUploadConfiguration.ErrorHandler += JoinServiceBatchConfiguration_ErrorHandler;
            config.InteractionUploadConfiguration.SuccessHandler += JoinServiceBatchConfiguration_SuccessHandler;
            this.features = new string[] { "a", "b", "c", "d" };
            this.freq = new Dictionary<string, int>();
            this.rnd = new Random(123);

            // reset the model
            this.OnlineTrainerReset();

            Console.WriteLine("Waiting after reset...");
            Thread.Sleep(TimeSpan.FromSeconds(2));

            {
                var expectedEvents = 0;
                using (var client = Microsoft.Research.MultiWorldTesting.ClientLibrary.DecisionService.Create<MyContext>(config))
                {
                    for (int i = 0; i < 100; i++)
                    {
                        expectedEvents += SendEvents(client, 128);
                        // Thread.Sleep(500);                        
                    }
                }

                // TODO: flush doesn't work
                // Assert.AreEqual(expectedEvents, this.eventCount);
            }

            // 4 actions times 4 feature values
            Assert.AreEqual(4 * 4, freq.Keys.Count);

            Console.WriteLine("Exploration");
            var total = freq.Values.Sum();
            foreach (var k in freq.Keys.OrderBy(k => k))
            {
                var f = freq[k] / (float)total;
                Assert.IsTrue(f < 0.08);
                Console.WriteLine("{0} | {1}", k, f);
            }

            freq.Clear();

            // TODO: update eps: 0
            using (var client = Microsoft.Research.MultiWorldTesting.ClientLibrary.DecisionService.Create<MyContext>(config))
            {
                int i;
                for (i = 0; i < 120; i++)
                {
                    try
                    {
                        client.DownloadModelAndUpdate(new System.Threading.CancellationToken()).Wait();
                        break;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }

                Assert.IsTrue(i < 30, "Unable to download model");

                for (i = 0; i < 1024; i++)
                {
                    var key = Guid.NewGuid().ToString();

                    var featureIndex = i % features.Length;

                    var action = client.ChooseAction(key, new MyContext { Feature = features[featureIndex] });

                    var stat = string.Format("'{0}' '{1}' ", features[featureIndex], action);
                    int count;
                    if (freq.TryGetValue(stat, out count))
                        freq[stat]++;
                    else
                        freq.Add(stat, count);
                }
            }

            Console.WriteLine("Exploitation");
            total = freq.Values.Sum();
            foreach (var k in freq.Keys.OrderBy(k => k))
            {
                var f = freq[k] / (float)total;
                Assert.AreEqual(0.25f, f, 0.1);
                Console.WriteLine("{0} | {1}", k, f);
            }
        }

        void JoinServiceBatchConfiguration_SuccessHandler(object source, int eventCount, int sumSize, int inputQueueSize)
        {
            this.eventCount += eventCount;
        }

        void JoinServiceBatchConfiguration_ErrorHandler(object source, Exception e)
        {
            Assert.Fail("Exception during upload: " + e.Message);
        }

        public class MyContext
        {
            public string Feature { get; set; }
        }

        private int SendEvents(DecisionServiceClient<MyContext> client, int numberOfEvents)
        {
            var expectedEvents = 0;

            for (int i = 0; i < numberOfEvents; i++)
            {
                var key = Guid.NewGuid().ToString();

                var featureIndex = i % features.Length;

                var action = client.ChooseAction(key, new MyContext { Feature = features[featureIndex] });
                expectedEvents++;

                // Feature | Action
                //    A   -->  1
                //    B   -->  2
                //    C   -->  3
                //    D   -->  4
                // only report in 50% of the cases
                if (rnd.NextDouble() < .75 && action - 1 == featureIndex)
                {
                    client.ReportReward(2, key);
                    expectedEvents++;
                }

                var stat = string.Format("'{0}' '{1}' ", features[featureIndex], action);
                int count;
                if (freq.TryGetValue(stat, out count))
                    freq[stat]++;
                else
                    freq.Add(stat, count);
            }

            return expectedEvents;
        }
    }
}
