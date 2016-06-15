using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Text;
using System.Globalization;

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
        readonly string authToken = "qig2esedxdvx6"; // insert auth token
        readonly string baseUrl = "https://dmdp2-webapi-hjus63xwh2t6y.azurewebsites.net/"; // insert API URL here

        readonly int numActions = 3;

        [TestMethod]
        public void IndexExistsTest()
        {
            // Arrange
            var wc = new WebClient();
            var indexUrl = baseUrl + "index.html";
            var response = wc.DownloadString(indexUrl);

            Assert.AreEqual("<!DOCTYPE html>", response.Substring(0,15));
        }

        [TestMethod]
        public void PostTest()
        {
            // Arrange
            var wc = new WebClient();
            //wc.Headers.Add("Content-type: application/json");
            // insert webAPI auth token here
            wc.Headers.Add("Authorization", authToken);

            // Act
            // string requestUri;

            string requestUri = string.Format(CultureInfo.InvariantCulture, "{0}/api/decision?numActions={1}", baseUrl, numActions);
            wc.QueryString.Add("Age", "34");
            wc.QueryString.Add("Location", "Seattle");
            //MyContext cxt = new MyContext { Age = 34, Location = "Seattle" };
            //string encodedCxt = JsonConvert.SerializeObject(cxt);
            var response = wc.UploadValues(requestUri, "POST", wc.QueryString);

            var utf8response = UnicodeEncoding.UTF8.GetString(response);

            // Assert
            Assert.AreEqual("foo-tbd", utf8response);


            // wait for user keypress
            Console.ReadLine();
        }


        [TestMethod]
        public void ThroughputTest()
        {
            // stub

        }
    }
}
