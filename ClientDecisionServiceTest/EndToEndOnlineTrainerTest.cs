using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class EndToEndOnlineTrainerTestClass
    {
        public class MyContext
        {
            public string Feature { get; set; }
        }

        private int eventCount;

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


        private Dictionary<string, int> freq;
        private string[] features;
        private Random rnd;

        [TestMethod]
        public void EndToEndOnlineTrainerTest()
        {
            // configure VW parameters: 

            var uri = "https://mcdel8storage.blob.core.windows.net/mwt-settings/client";

            var metaData = ApplicationMetadataUtil.DownloadMetadata<ApplicationClientMetadata>(uri);
            Assert.IsTrue(metaData.TrainArguments.Contains("--cb 4"));
            
            // 4 Actions
            var config = new DecisionServiceConfiguration(uri)
            {
                InteractionUploadConfiguration = new Microsoft.Research.MultiWorldTesting.JoinUploader.BatchingConfiguration
                {
                    MaxEventCount = 64
                },
                ObservationUploadConfiguration = new Microsoft.Research.MultiWorldTesting.JoinUploader.BatchingConfiguration
                {
                    MaxEventCount = 64
                }
            };
            
            config.InteractionUploadConfiguration.ErrorHandler +=JoinServiceBatchConfiguration_ErrorHandler;
            config.InteractionUploadConfiguration.SuccessHandler +=JoinServiceBatchConfiguration_SuccessHandler;
            this.features = new string[] { "a", "b", "c", "d" };
            this.freq = new Dictionary<string, int>();
            this.rnd = new Random(123);

            //// reset the model
                var wc = new WebClient();
                wc.Headers.Add("Authorization: a");
                wc.DownloadString("http://127.0.0.1:81/reset");

            //Trace.TraceInformation("Waiting after reset...");
            //Thread.Sleep(TimeSpan.FromSeconds(5));
            
            {
                var expectedEvents = 0;
                using (var client = DecisionService.Create<MyContext>(config)
                    .WithEpsilonGreedy(1f))
                {
                    // need to send events for at least experimental unit duration, so ASA is triggered
                    for (int i = 0; i < 100; i++)
                    {
                        expectedEvents += SendEvents(client, 128);
                        // Thread.Sleep(500);                        
                    }
                }

                Assert.AreEqual(expectedEvents, this.eventCount);
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
            using (var client = DecisionService.Create<MyContext>(config))
            {
                int i;
                for (i = 0; i < 120; i++)
                {
                    try
                    {
                        client.DownloadModelAndUpdate(new System.Threading.CancellationToken()).Wait();
                        break;
                    }
                    catch (Exception e)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
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
    }
}

