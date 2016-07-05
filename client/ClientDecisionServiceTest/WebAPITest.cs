using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Text;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace ClientDecisionServiceTest
{
    public class MyContext
    {
        // Feature: Age:25
        public int Age { get; set; }

        // Feature: l:New_York
        [JsonProperty("l")]
        public string Location { get; set; }

        // Logged, but not used as feature due to leading underscore
        [JsonProperty("_isMember")]
        public bool IsMember { get; set; }

        // Not logged, not used as feature due to JsonIgnore
        [JsonIgnore]
        public string SessionId { get; set; }
    }

    // TODO: move to ds-provisioning
    [TestClass]
    [Ignore]
    public class WebApiTest
    {
        static string ADFUrl = "http://dmdp1-webapi-jvj7wdftvsdwe.azurewebsites.net";
        static string ADFAuthToken = "mzf2xsxf4hjwe";
        static string nonADFUrl = "http://dmdp6-webapi-q3wdu6gx5gnxm.azurewebsites.net/";
        static string nonADFAuthToken = "d2t67awl6jdre";
        // static int numActions = 3;

        WebClient wc;

        public WebApiTest()
        {
            wc = new WebClient();
        }

        [TestMethod]
        [Ignore]
        public void IndexExistsTest()
        {
            var indexUrl = ADFUrl + "index.html";
            var response = wc.DownloadString(indexUrl);

            Assert.AreEqual("<!DOCTYPE html>", response.Substring(0,15));
        }

        public JObject InteractionParts1and2(string baseUrl, string contextType, string contextString)
        {
            string contextUri = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", baseUrl, contextType);
            byte[] context = System.Text.Encoding.ASCII.GetBytes(contextString);
            var response = wc.UploadData(contextUri, "POST", context);
            var utf8response = UnicodeEncoding.UTF8.GetString(response);
            JObject responseJObj = JObject.Parse(utf8response);
            return responseJObj;
        }

        public string InteractionPart3(string baseUrl, JObject responseJObj, float reward)
        {
            string eventID = (string)responseJObj["EventId"];
            string rewardUri = string.Format(CultureInfo.InvariantCulture, "{0}/reward/{1}", baseUrl, eventID);
            string rewardString = reward.ToString();
            byte[] rewardBytes = System.Text.Encoding.ASCII.GetBytes(rewardString);
            var response = wc.UploadData(rewardUri, "POST", rewardBytes);
            string utf8response = UnicodeEncoding.UTF8.GetString(response);
            return utf8response;
        }

        [TestMethod]
        [Ignore]
        public void ADFPostTest()
        {
            wc.Headers.Clear();
            wc.Headers.Add("Authorization", ADFAuthToken);
            string baseUrl = ADFUrl;
            // send context, and receive decision(s)
            string contextType = "ranker";
            string contextString = "{ Age: 25, Location: \"New York\", _multi: [{ a: 1}, { b: 2}]}";
            JObject responseJObj = InteractionParts1and2(baseUrl, contextType, contextString);

            // parse decision
            JArray actions = (JArray)responseJObj["Action"];
            int topAction = (int)actions[0];

            // Compare only the decision, not the eventID
            Assert.IsTrue(topAction >= 1);

            // now post the reward
            float reward = 1.0F;
            string utf8response = InteractionPart3(baseUrl, responseJObj, reward);

            // parse response to reward (should be empty)
            Assert.AreEqual("", utf8response);
        }

        [TestMethod]
        [Ignore]
        public void nonADFPostTest()
        {
            wc.Headers.Clear();
            wc.Headers.Add("Authorization", nonADFAuthToken);
            string baseUrl = nonADFUrl;

            // send context, and receive decision(s)
            string contextType = "policy";
            string contextString = "{ Age: 25, Location: \"New York\"}";
            JObject responseJObj = InteractionParts1and2(baseUrl, contextType, contextString);

            // parse decision
            int topAction = (int)responseJObj["Action"];

            // Compare only the decision, not the eventID
            Assert.IsTrue(topAction >= 1);

            // now post the reward
            float reward = 1.0F;
            string utf8response = InteractionPart3(baseUrl, responseJObj, reward);

            // parse response to reward (should be empty)
            Assert.AreEqual("", utf8response);
        }

        [TestMethod]
        [Ignore]
        public void ThroughputTest()
        {
            wc.Headers.Clear();
            wc.Headers.Add("Authorization", nonADFAuthToken);
            string baseUrl = nonADFUrl;

            // send context, and receive decision(s)
            string contextType = "policy";
            string contextString = "{ Age: 25, Location: \"New York\"}";


            DateTime start = DateTime.Now;
            for (int i = 0; i < 1000; ++i)
            {
                JObject responseJObj = InteractionParts1and2(baseUrl, contextType, contextString);

                // parse decision
                int topAction = (int)responseJObj["Action"];

                // Compare only the decision, not the eventID
                Assert.IsTrue(topAction >= 1);

                // now post the reward
                float reward = 1.0F;
                string utf8response = InteractionPart3(baseUrl, responseJObj, reward);
            }
            var duration = (DateTime.Now - start).TotalSeconds;

            // parse response to reward (should be empty)
            Assert.IsTrue(duration < 100);
        }
    }
}
