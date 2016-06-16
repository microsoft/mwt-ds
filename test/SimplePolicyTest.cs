using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Research.DecisionServiceTest
{
    [TestClass]
    public class SimplePolicyTestClass : ProvisioningBaseTest
    {
        private const string deploymentOutput = @"
{
  ""management Center URL"": {
    ""type"": ""String"",
    ""value"": ""https://mcunitd5395c8845b9951fe786e802964a-mc-dz564t66ivmi2.azurewebsites.net""
  },
  ""management Center Password"": {
    ""type"": ""String"",
    ""value"": ""4uwwocm5jwwha""
  },
  ""client Library Url"": {
    ""type"": ""String"",
    ""value"": ""https://storagedz564t66ivmi2.blob.core.windows.net/mwt-settings/client?sv=2015-04-05&sr=b&sig=3hILsSaLwF9B0ZeuAokvqEfhenv%2BJjeIxBCY4sjRHwU%3D&st=2016-06-16T13%3A37%3A00Z&se=2017-06-16T13%3A38%3A00Z&sp=r""
  },
  ""web Service URL"": {
    ""type"": ""String"",
    ""value"": ""https://mcunitd5395c8845b9951fe786e802964a-webapi-dz564t66ivmi2.azurewebsites.net""
  },
  ""web Service Token"": {
    ""type"": ""String"",
    ""value"": ""ii2wmkr677njk""
  },
  ""online Trainer Token"": {
    ""type"": ""String"",
    ""value"": ""l62oo233f3zig""
  },
  ""online Trainer URL"": {
    ""type"": ""String"",
    ""value"": ""http://mcunitd5395c8845b9951fe786e802964a-trainer-dz564t66ivmi2.cloudapp.net""
  }
}
";

        private Dictionary<string, int> freq;
        private string[] features;
        private Random rnd;
        private int eventCount;

        public SimplePolicyTestClass() : base(deploymentOutput) { }

        [TestMethod]
        public async Task SimplePolicyTest()
        {
            this.ConfigureDecisionService("--cb_explore 4 --epsilon 0", initialExplorationEpsilon:1, isExplorationEnabled: true);

            // 4 Actions
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
                    // need to send events for at least experimental unit duration, so ASA is triggered
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
