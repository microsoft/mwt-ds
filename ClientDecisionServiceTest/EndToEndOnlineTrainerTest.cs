using Microsoft.Research.MultiWorldTesting.ClientLibrary;
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

        private int SendEvents(DecisionServiceClient<MyContext, int, int> client, int numberOfEvents)
        {
            var expectedEvents = 0;

            for (int i = 0; i < numberOfEvents; i++)
            {
                var key = new UniqueEventID() { Key = Guid.NewGuid().ToString(), TimeStamp = DateTime.UtcNow };

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
        [Ignore]
        public void EndToEndOnlineTrainerTest()
        {
            var token = "c609ecb2-80ff-4763-bb85-5e171567e067";
            // 4 Actions
            //var config = new DecisionServiceConfiguration("2fee5489-0b9f-4590-9cbe-eee9802e1e3c");
            var config = new DecisionServiceConfiguration(token)
            {
                JoinServiceBatchConfiguration = new Microsoft.Research.MultiWorldTesting.JoinUploader.BatchingConfiguration()
            };
            
            config.JoinServiceBatchConfiguration.ErrorHandler +=JoinServiceBatchConfiguration_ErrorHandler;
            config.JoinServiceBatchConfiguration.SuccessHandler +=JoinServiceBatchConfiguration_SuccessHandler;
            this.features = new string[] { "a", "b", "c", "d" };
            this.freq = new Dictionary<string, int>();
            this.rnd = new Random(123);

            // reset the model
            var wc = new WebClient();
            wc.Headers.Add("Authorization: " + token);
            wc.DownloadString("http://127.0.0.1:81/onlineTrainer");

            Trace.TraceInformation("Waiting after reset...");
            Thread.Sleep(10000);
            
            {
                var expectedEvents = 0;

                using (var client = DecisionService.WithPolicy(config, numberOfActions: 4).With<MyContext>()
                    .WithEpsilonGreedy(1f))
                {
                    // need to send events for at least experimental unit duration, so ASA is triggered
                    for (int i = 0; i < 12; i++)
                    {
                        expectedEvents += SendEvents(client, 1024);
                        Thread.Sleep(1000);                        
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

            using (var client = DecisionService.WithPolicy(config, numberOfActions: 4).With<MyContext>()
                .WithEpsilonGreedy(epsilon: 0))
            {
                // TODO: this needs to block until the model is actually available (or at least loop and poll)
                int i;
                for (i = 0; i < 60; i++)
                {
                    try
                    {
                        client.DownloadModelAndUpdate(new System.Threading.CancellationToken()).Wait();

                        break;
                    }
                    catch (Exception e)
                    {
                        Thread.Sleep(1000);
                    }
                }

                Assert.IsTrue(i < 30, "Unable to download model");

                for (i = 0; i < 1024; i++)
                {
                    var key = new UniqueEventID() { Key = Guid.NewGuid().ToString(), TimeStamp = DateTime.UtcNow };

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

