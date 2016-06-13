using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

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

    [TestClass]
    public class WebApiTest
    {
        string authToken = "t4izmcj43icxi"; // insert auth token
        string baseUrl = "http://dmforkdp2-webapi.azurewebsites.net/"; // insert API URL here

        int numActions = 3;

        string requestUri;

        [TestMethod]
        public void HeartBeatTest()
        {
            // Arrange
            var wc = new WebClient();
            wc.Headers.Add("Content-type: application/json");
            // insert webAPI auth token here
            wc.Headers.Add("Authorization", authToken);

            // Act
            requestUri = String.Format("https://{0}/api/decision?numActions={1}", baseUrl, numActions);

            MyContext cxt = new MyContext { Age = 34, Location = "Seattle" };
            string encodedCxt = JsonConvert.SerializeObject(cxt);
            var response = wc.UploadString(requestUri, encodedCxt);

            // var response = UnicodeEncoding.UTF8.GetString(data);
            Console.WriteLine(response);

            // Assert
            Assert.AreEqual(response, "foo-tbd");


            // wait for user keypress
            Console.ReadLine();
        }


        [TestMethod]
        public void ThroughputTest()
        {


        }
    }
}
