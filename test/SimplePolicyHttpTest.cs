using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text;
using System.Net;

namespace Microsoft.Research.DecisionServiceTest
{
    [TestClass]
    public class SimplePolicyHttpTestClass : ProvisioningBaseTest
    {
        private const string deploymentOutput = @"
{
  ""management Center URL"": {
    ""type"": ""String"",
    ""value"": ""https://dmunitef1924c44cc9b28ff02c1fe6650b-mc-a5u7oqwv2xzi4.azurewebsites.net""
  },
  ""management Center Password"": {
    ""type"": ""String"",
    ""value"": ""dkqwm7lmlgipi""
  },
  ""client Library Url"": {
    ""type"": ""String"",
    ""value"": ""https://storagea5u7oqwv2xzi4.blob.core.windows.net/mwt-settings/client?sv=2015-07-08&sr=b&sig=I9W6J8HabJnjYiKilrAGLTb8Fo3yULI7raXuBohsm3M%3D&st=2016-06-20T04%3A11%3A44Z&se=2017-06-20T04%3A12%3A44Z&sp=r""
  },
  ""online Trainer Token"": {
    ""type"": ""String"",
    ""value"": ""7cbzr4zikh6ko""
  },
  ""online Trainer URL"": {
    ""type"": ""String"",
    ""value"": ""http://dmunitef1924c44cc9b28ff02c1fe6650b-trainer-a5u7oqwv2xzi4.cloudapp.net""
  }
}";
        private const string contextType = "policy";

        private Dictionary<string, int> freq;
        private string[] features;
        private Random rnd;
        private int eventCount;

        WebClient wc;

        public SimplePolicyHttpTestClass()
         : base(deploymentOutput) 
        {
            wc = new WebClient();
            wc.Headers.Add("auth", managementPassword);
        }

        [TestMethod]
        public async Task SimplePolicyHttpTest()
        {
            this.ConfigureDecisionService("--cb_explore 4 --epsilon 0", initialExplorationEpsilon: 1, isExplorationEnabled: true);

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

            Console.WriteLine("Exploration");
            var expectedEvents = 0;
            for (int i = 0; i < 128; i++)
            {
                int featureIndex = i % features.Length;
                expectedEvents += SendEvents(wc, featureIndex);
            }
            // Thread.Sleep(500);                        
            // TODO: flush doesn't work
            // Assert.AreEqual(expectedEvents, this.eventCount);

            // 4 actions times 4 feature values
            Assert.AreEqual(4 * 4, freq.Keys.Count);

            var total = freq.Values.Sum();
            foreach (var k in freq.Keys.OrderBy(k => k))
            {
                var f = freq[k] / (float)total;
                Assert.IsTrue(f < 0.08);
                Console.WriteLine("{0} | {1}", k, f);
            }

            freq.Clear();

            // TODO: update eps to 0

            // check here to make sure model was updated
            Console.WriteLine("Exploitation");
            expectedEvents = 0;
            for (int i = 0; i < 1024; i++)
            {
                var featureIndex = i % features.Length;
                expectedEvents += SendEvents(wc, featureIndex, false);
            }

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

        public JObject InteractionParts1and2(string contextType, string contextString)
        {
            string contextUri = string.Format(CultureInfo.InvariantCulture, "{0}/API/{1}", managementCenterUrl, contextType);
            byte[] context = System.Text.Encoding.ASCII.GetBytes(contextString);
            var response = wc.UploadData(contextUri, "POST", context);
            var utf8response = UnicodeEncoding.UTF8.GetString(response);
            JObject responseJObj = JObject.Parse(utf8response);
            return responseJObj;
        }

        public string InteractionPart3(JObject responseJObj, float reward)
        {
            string eventID = (string)responseJObj["EventId"];
            string rewardUri = string.Format(CultureInfo.InvariantCulture, "{0}/API/reward/?eventId={1}", managementCenterUrl, eventID);
            string rewardString = reward.ToString();
            byte[] rewardBytes = System.Text.Encoding.ASCII.GetBytes(rewardString);
            var response = wc.UploadData(rewardUri, "POST", rewardBytes);
            string utf8response = UnicodeEncoding.UTF8.GetString(response);
            return utf8response;
        }

        private int SendEvents(WebClient client, int featureIndex, bool sendReward = true)
        {
            const float reward = 2.0F;

            var expectedEvents = 0;
            string contextString = $"{{a: \"{features[featureIndex]}\"}}";
            var responseJObj = InteractionParts1and2(contextType, contextString);
            int action = (int)responseJObj["Action"];

            // Feature | Action
            //    A   -->  1
            //    B   -->  2
            //    C   -->  3
            //    D   -->  4
            // only report in 50% of the cases
            if (sendReward && rnd.NextDouble() < .75 && action - 1 == featureIndex)
            {
                InteractionPart3(responseJObj, reward);
                expectedEvents = 1;
            }

            var stat = string.Format("'{0}' '{1}' ", features[featureIndex], action);
            int count;
            if (freq.TryGetValue(stat, out count))
                freq[stat]++;
            else
                freq.Add(stat, count);

            return expectedEvents;
        }

    }
}