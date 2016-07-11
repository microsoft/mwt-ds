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
    ""value"": ""https://mcdel22-mc-vzho3vjk67ppe.azurewebsites.net""
  },
  ""management Center Password"": {
    ""type"": ""String"",
    ""value"": ""iab5lxbsptvp2""
  },
  ""client Library URL"": {
    ""type"": ""String"",
    ""value"": ""https://storagevzho3vjk67ppe.blob.core.windows.net/mwt-settings/client?sv=2015-07-08&sr=b&sig=hZ3O111eqfCuyLIXV4LKPR0UMiNaweKhTDEIBephGYM%3D&st=2016-06-23T20%3A19%3A19Z&se=2017-06-23T20%3A20%3A19Z&sp=r""
  },
  ""web Service Token"": {
    ""type"": ""String"",
    ""value"": ""wgxm34xrnhnd4""
  },
  ""online Trainer Token"": {
    ""type"": ""String"",
    ""value"": ""6n6ucxhzebevw""
  },
  ""online Trainer URL"": {
    ""type"": ""String"",
    ""value"": ""http://mcdel22-trainer-vzho3vjk67ppe.cloudapp.net""
  }
}
";
        // public APITestDriveTestClass() : base(deploymentOutput) { }

        public class PolicyDecision
        {
            public string EventId { get; set; }

            public int Action { get; set; }

            public string ModelTime { get; set; }
        }

        [TestMethod]
        [TestCategory("End to End")]
        [Priority(2)]
        [Ignore]
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

                for (int i = 0; i < 32*1024; i++)
                {
                    var index = rnd.Next(2);

                    // mapping
                    var expectedAction = index;
                    if (rnd.NextDouble() < .2)
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
                            Trace.WriteLine($"Location {locs[j],-9}: {confusionMatrix[j, 0],-4} {confusionMatrix[j, 1],-4}");

                        // float sum = confusionMatrix.OfType<int>().Sum();
                        for (int j = 0; j < 2; j++)
                        {
                            var a1 = confusionMatrix[j, 0];
                            var a2 = confusionMatrix[j, 1];
                            float sum = a1 + a2;
                            Trace.WriteLine($"Location {locs[j],-9}: {a1/sum:0.00} {a2 / sum:0.00}");
                        }
                        Trace.WriteLine("");
                    }
                }
            }
        }
    }
}
