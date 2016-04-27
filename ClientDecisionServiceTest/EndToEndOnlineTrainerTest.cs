using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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

        [TestMethod]
        //[Ignore]
        public void EndToEndOnlineTrainerTest()
        {
            // 4 Actions
            var config = new DecisionServiceConfiguration("2fee5489-0b9f-4590-9cbe-eee9802e1e3c")
            {
                JoinServiceBatchConfiguration = new Microsoft.Research.MultiWorldTesting.JoinUploader.BatchingConfiguration
                {
                    MaxUploadQueueCapacity = 16*1024
                }
            };

            var features = new string[] { "a", "b", "c", "d" };
            var freq = new Dictionary<string, int>();

            var rnd = new Random(123);

            // reset the model
            //var wc = new WebClient();
            //wc.Headers.Add("Authorization: 2fee5489-0b9f-4590-9cbe-eee9802e1e3c");
            //wc.DownloadString("http://mcapp3-trainer.cloudapp.net/onlineTrainer");
            //Thread.Sleep(5000);

            while (true)
            {
                using (var client = DecisionService.WithPolicy(config, numberOfActions: 4).With<MyContext>()
                    .WithEpsilonGreedy(1f))
                {
                    for (int i = 0; i < 8 * 1024; i++)
                    {
                        var key = new UniqueEventID() { Key = Guid.NewGuid().ToString(), TimeStamp = DateTime.UtcNow };

                        var featureIndex = i % features.Length;

                        var action = client.ChooseAction(key, new MyContext { Feature = features[featureIndex] });

                        // Feature | Action
                        //    A   -->  1
                        //    B   -->  2
                        //    C   -->  3
                        //    D   -->  4
                        // only report in 50% of the cases
                        if (rnd.NextDouble() < .75 && action - 1 == featureIndex)
                            client.ReportReward(2, key);

                        var stat = string.Format("'{0}' '{1}' ", features[featureIndex], action);
                        int count;
                        if (freq.TryGetValue(stat, out count))
                            freq[stat]++;
                        else
                            freq.Add(stat, count);
                    }
                }

                Console.WriteLine("abc");
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
                client.DownloadModelAndUpdate(new System.Threading.CancellationToken()).Wait();

                for (int i = 0; i < 1024; i++)
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
    }
}

