using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using Microsoft.Rest;
using Microsoft.Azure.Management.ResourceManager;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure;
using Microsoft.Azure.Management.ResourceManager.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Configuration;
using System.Net;
using System.Text;
using System.Web;
using Microsoft.Research.MultiWorldTesting.Contract;
using System.Threading;

namespace Microsoft.Research.DecisionServiceTest
{
    [TestClass]
    public partial class ProvisioningTest
    {
        [TestMethod]
        [Ignore]
        public void ProvisionOnlyTest()
        {
            this.Initialize();

            Assert.IsNotNull(this.managementCenterUrl);
            Assert.IsNotNull(this.managementPassword);
            Assert.IsNotNull(this.onlineTrainerUrl);
            Assert.IsNotNull(this.onlineTrainerToken);
            Assert.IsNotNull(this.webServiceToken);
            Assert.IsNotNull(this.settingsUrl);
        }

        [TestMethod]
        [TestCategory("End to End")]
        [Priority(2)]
        public async Task AllEndToEndTests()
        {
            this.Initialize();

            await SimplePolicyTest();

            E2ERankerStochasticRewards();
        }


        private JObject deploymentOutput;

        protected bool deleteOnCleanup;
        protected string managementCenterUrl;
        protected string managementPassword;
        protected string onlineTrainerUrl;
        protected string onlineTrainerToken;
        protected string webServiceToken;
        protected string settingsUrl;

        private static string GetConfiguration(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (value != null)
                return value;

            return ConfigurationManager.AppSettings[name];
        }

        public ProvisioningTest()
        {
//            string deploymentOutput = @"
//{
//  ""management Center URL"": {
//    ""type"": ""String"",
//    ""value"": ""https://mc-sccwor75dvlcuchl6tlbcaux42.azurewebsites.net""
//  },
//  ""management Center Password"": {
//    ""type"": ""String"",
//    ""value"": ""vmfhd4lsmxkbk""
//  },
//  ""client Library URL"": {
//    ""type"": ""String"",
//    ""value"": ""https://storagesccwor75dvlcu.blob.core.windows.net/mwt-settings/client?sv=2015-07-08&sr=b&sig=lre%2BGTE9wfgXucIR62FAY8NiQQEADgbq2x26ur3bCsA%3D&st=2016-07-11T17%3A59%3A04Z&se=2017-07-11T18%3A00%3A04Z&sp=r""
//  },
//  ""web Service Token"": {
//    ""type"": ""String"",
//    ""value"": ""57dx6h2tw464k""
//  },
//  ""online Trainer Token"": {
//    ""type"": ""String"",
//    ""value"": ""votzwbdgrkcoe""
//  },
//  ""online Trainer URL"": {
//    ""type"": ""String"",
//    ""value"": ""http://trainer-sccwor75dvlcuchl6tlbcaux42.cloudapp.net""
//  }
//}
//";
//            this.deploymentOutput = JObject.Parse(deploymentOutput);
//            this.ParseDeploymentOutputs();

            this.deleteOnCleanup = false;
        }



        protected void ConfigureDecisionService(string trainArguments = null, float? initialExplorationEpsilon = null, bool? isExplorationEnabled = null)
        {
            using (var wc = new WebClient())
            {
                wc.Headers.Add($"auth: {managementPassword}");
                var query = new List<string>();
                if (trainArguments != null)
                    query.Add("trainArguments=" + HttpUtility.UrlEncode(trainArguments));
                if (initialExplorationEpsilon != null)
                    query.Add("initialExplorationEpsilon=" + initialExplorationEpsilon);
                if (isExplorationEnabled != null)
                    query.Add("isExplorationEnabled=" + isExplorationEnabled);

                var url = managementCenterUrl + "/Automation/UpdateSettings";
                if (query.Count > 0)
                    url += "?" + string.Join("&", query);

                wc.DownloadString(url);
            }

            // validate
            var metaData = ApplicationMetadataUtil.DownloadMetadata<ApplicationClientMetadata>(settingsUrl);
            if (trainArguments != null)
                Assert.AreEqual(trainArguments, metaData.TrainArguments);

            if (initialExplorationEpsilon != null)
                Assert.AreEqual((float)initialExplorationEpsilon, metaData.InitialExplorationEpsilon);

            if (isExplorationEnabled != null)
                Assert.AreEqual((bool)isExplorationEnabled, metaData.IsExplorationEnabled);
        }

        protected void OnlineTrainerWaitForStartup()
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < TimeSpan.FromMinutes(20))
            {
                try
                {
                    using (var wc = new WebClient())
                    {
                        wc.DownloadString($"{onlineTrainerUrl}/status");
                    }
                    return;
                }
                catch (Exception)
                {
                }

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }

        protected void OnlineTrainerReset()
        {
            using (var wc = new WebClient())
            {
                wc.Headers.Add($"Authorization: {onlineTrainerToken}");
                wc.DownloadString($"{onlineTrainerUrl}/reset");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }

        private string GetDeploymentOutput(string name)
        {
            foreach (var output in this.deploymentOutput)
            {
                if (output.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return output.Value["value"].ToObject<string>();
            }

            return null;
        }

        private void ParseDeploymentOutputs()
        {
            this.managementCenterUrl = this.GetDeploymentOutput("Management Center URL");
            this.managementPassword = this.GetDeploymentOutput("Management Center Password");
            this.onlineTrainerUrl = this.GetDeploymentOutput("Online Trainer URL");
            this.onlineTrainerToken = this.GetDeploymentOutput("Online Trainer Token");
            this.webServiceToken = this.GetDeploymentOutput("Web Service Token");
            this.settingsUrl = this.GetDeploymentOutput("Client Library URL");
        }

        private ResourceManagementClient CreateResourceManagementClient()
        {
            string clientId = GetConfiguration("ClientId");
            string passwd = GetConfiguration("Password");
            string tenantId = GetConfiguration("TenantId");
            string subscriptionId = GetConfiguration("SubscriptionId");

            var clientCredentials = new ClientCredential(clientId, passwd);
            var authorityUri = "https://login.windows.net/" + tenantId;
            var context = new AuthenticationContext(authorityUri);
            var authResult = context.AcquireTokenAsync("https://management.azure.com/", clientCredentials);
            authResult.Wait();

            var tokenCredentials = new TokenCloudCredentials(subscriptionId, authResult.Result.AccessToken);
            var credentialsForRM = new TokenCredentials(tokenCredentials.Token);

            var armClient = new ResourceManagementClient(credentialsForRM);
            armClient.SubscriptionId = subscriptionId;

            return armClient;
        }

        public void Initialize()
        {
            // re-using deployment
            if (this.deploymentOutput != null)
                return;

            Cleanup();

            var armClient = this.CreateResourceManagementClient();
            
            var prefix = GetConfiguration("prefix");

            var resourceGroupName = prefix + Guid.NewGuid().ToString().Replace("-","").Substring(4);
            var deploymentName = resourceGroupName + "_deployment";
            var location = "eastus";

            // create resource group
            var rgResult = armClient.ResourceGroups.CreateOrUpdate(resourceGroupName, new ResourceGroup { Location = location });

            Assert.AreEqual("Succeeded", rgResult.Properties.ProvisioningState);

            DeploymentOperation failedOperation = null;
            using (var poller = Observable
                .Interval(TimeSpan.FromSeconds(2))
                .Delay(TimeSpan.FromSeconds(5))
                .SelectMany(async _ =>
                {
                    try
                    {
                        var operations = await armClient.DeploymentOperations.ListAsync(resourceGroupName, deploymentName);

                        // {op.Properties.TargetResource.Id}
                        foreach (var op in operations)
                        {
                            if (op == null || op.Properties == null || op.Properties.ProvisioningState == null)
                                continue;

                            Trace.WriteLine($"Status: {op?.Properties?.TargetResource?.ResourceName,-30} '{op?.Properties?.ProvisioningState}'");
                            switch(op.Properties.ProvisioningState)
                            {
                                case "Running":
                                case "Succeeded":
                                    break;
                                default:
                                    failedOperation = op;
                                    break;
                            }
                        }
                        Trace.WriteLine("");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine("Status polling failed: " + ex.Message);
                    }

                    return Task.FromResult(true);
                })
                .Subscribe())
            {
                var deployment = new Deployment();
                deployment.Properties = new DeploymentProperties
                {
                    Mode = DeploymentMode.Incremental,
                    TemplateLink = new TemplateLink("https://raw.githubusercontent.com/Microsoft/mwt-ds/master/provisioning/azuredeploy.json"),
                    // Parameters = JObject.Parse("{\"param\":{\"subtemplate location\":\"https://raw.githubusercontent.com/eisber/mwt-ds/master/provisioning/\"}}")
                };

                try
                {
                    var dpResult = armClient.Deployments.CreateOrUpdate(resourceGroupName, deploymentName, deployment);
                    this.deploymentOutput = (JObject)dpResult.Properties.Outputs;

                    // make test case copy paste easy
                    Console.WriteLine(deploymentOutput.ToString(Newtonsoft.Json.Formatting.Indented).Replace("\"", "\"\""));

                    this.ParseDeploymentOutputs();
                }
                catch (AggregateException ae)
                {
                    // Go through all exceptions and dump useful information
                    ae.Handle(x =>
                    {
                        Trace.WriteLine(x);
                        return false;
                    });
                }
            }

            Assert.IsNull(failedOperation, $"Deployment operation failed: '{failedOperation}'");

            // give the deployment a bit of time to spin up completely
            Thread.Sleep(TimeSpan.FromMinutes(2));
        }

        //[TestCleanup]
        public void Cleanup()
        {
            if (!deleteOnCleanup)
                return;

            try
            {
                var armClient = this.CreateResourceManagementClient();
                var prefix = GetConfiguration("prefix");
                var oldResources = armClient.ResourceGroups.List().Where(r => r.Name.StartsWith(prefix));

                foreach (var resource in oldResources)
                {
                    var task = armClient.ResourceGroups.DeleteAsync(resource.Name);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
