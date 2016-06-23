using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.DecisionServiceTest
{
    [TestClass]
    public class APITestDriveTestClass : ProvisioningBaseTest
    {
        private const string deploymentOutput = @"
{
  ""management Center URL"": {
    ""type"": ""String"",
    ""value"": ""https://mcdel20-mc-45amhqirwzhww.azurewebsites.net""
  },
  ""management Center Password"": {
    ""type"": ""String"",
    ""value"": ""q26ifbbpulvgw""
  },
  ""client Library URL"": {
    ""type"": ""String"",
    ""value"": ""https://storageftupw3x6ndhxq.blob.core.windows.net/mwt-settings/client?sv=2015-07-08&sr=b&sig=BOgAw8%2Fxk7h7Rq5Qep2k1REmcLy0KNyU8ZbMaI%2F6FQI%3D&st=2016-06-20T17%3A16%3A39Z&se=2017-06-20T17%3A17%3A39Z&sp=r""
  },
  ""web Service Token"": {
    ""type"": ""String"",
    ""value"": ""b4wvknzqor57w""
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
        public APITestDriveTestClass() : base(deploymentOutput) { }

        public class PolicyDecision
        {
            public string EventId { get; set; }

            public int Action { get; set; }

            public string ModelTime { get; set; }
        }

        [TestMethod]
        public void APITestDriveTest()
        {
            var locs = new[] { "Seattle", "New York" };
            var genders = new[] { "Male", "Female" };
            var industries = new[] { "Tech", "Law" };
            var ages = new[] { "Young", "Old" };

            var rnd = new Random(123);

            var confusionMatrix = new int[2, 2];

            using (var wc = new WebClient())
            {
                wc.Headers.Add("auth: " + this.webServiceToken);
                var urlPolicy = $"{this.managementCenterUrl}/API/Policy";
                var urlReward = $"{this.managementCenterUrl}/API/Reward";

                for (int i = 0; i < 1000; i++)
                {
                    var index = rnd.Next(2);

                    // mapping
                    var expectedAction = index;
                    if (rnd.NextDouble() < .1)
                        expectedAction++;
                    expectedAction = (expectedAction % 2) + 1;

                    var jsonContext = JsonConvert.SerializeObject(new { Location = locs[index] });
                    var response = wc.UploadData(urlPolicy, "POST", Encoding.UTF8.GetBytes(jsonContext));
                    var decision = JsonConvert.DeserializeObject<PolicyDecision>(Encoding.UTF8.GetString(response));

                    if (decision.Action == expectedAction)
                        wc.UploadData($"{urlReward}/?eventId={decision.EventId}", "POST", Encoding.UTF8.GetBytes("1"));

                    confusionMatrix[index, decision.Action - 1]++;

                    Thread.Sleep(100);

                    if (i % 5 == 0)
                    {
                        Trace.WriteLine($"Model {decision.ModelTime}");
                        for (int j = 0; j < 2; j++)
                            Trace.WriteLine($"Location {locs[j],-15}: {confusionMatrix[j, 0],-4} {confusionMatrix[j, 1],-4}");
                        Trace.WriteLine("");
                    }
                }
            }
        }
    }
}
