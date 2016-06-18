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
    public class WebApiTest
    {
        readonly string authToken = "mzf2xsxf4hjwe"; // insert auth token
        readonly string baseUrl = "http://dmdp1-webapi-jvj7wdftvsdwe.azurewebsites.net"; // insert API URL here
        readonly string contextType = "ranker";
        // readonly int numActions = 3;

        [TestMethod]
        [Ignore]
        public void IndexExistsTest()
        {
            var wc = new WebClient();
            var indexUrl = baseUrl + "index.html";
            var response = wc.DownloadString(indexUrl);

            Assert.AreEqual("<!DOCTYPE html>", response.Substring(0,15));
        }

        [TestMethod]
        [Ignore]
        public void PostTest()
        {
            var wc = new WebClient();
            wc.Headers.Add("Authorization", authToken);

            string contextUri = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", baseUrl, contextType);
            string contextString = "{ Age: 25, Location: \"New York\", _multi: [{ a: 1}, { b: 2}]}";
            byte[] context = System.Text.Encoding.ASCII.GetBytes(contextString);
            var response = wc.UploadData(contextUri, "POST", context);

            var utf8response = UnicodeEncoding.UTF8.GetString(response);
            JObject jobj = JObject.Parse(utf8response);
            JArray actions = (JArray)jobj["Action"];
            int topAction = (int)actions[0];
            // Compare only the decision, not the eventID
            Assert.IsTrue(topAction >= 1);
            Assert.IsTrue(topAction <= 2);

            // now post the reward
            string eventID = (string)jobj["EventId"];
            string rewardUri = string.Format(CultureInfo.InvariantCulture, "{0}/reward/{1}", baseUrl, eventID);
            string rewardString = "1";
            byte[] reward = System.Text.Encoding.ASCII.GetBytes(rewardString);
            response = wc.UploadData(rewardUri, "POST", reward);
            utf8response = UnicodeEncoding.UTF8.GetString(response);

            Assert.AreEqual("", utf8response);
        }

        [TestMethod]
        [Ignore]
        public void ThroughputTest()
        {
            // stub

        }
    }
}
