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
    public class ProvisioningUtil
    {
        private JObject deploymentOutput;

        private static string GetConfiguration(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (value != null)
                return value;

            return ConfigurationManager.AppSettings[name];
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

        public DecisionServiceDeployment Deploy()
        {
            var armClient = this.CreateResourceManagementClient();

            var prefix = GetConfiguration("prefix");

            var resourceGroupName = prefix + Guid.NewGuid().ToString().Replace("-", "").Substring(4);
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
                            switch (op.Properties.ProvisioningState)
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
                    // Parameters = JObject.Parse("{\"param\":{\"subtemplate location\":\"https://raw.githubusercontent.com/Microsoft/mwt-ds/master/provisioning/\"}}")
                };

                try
                {
                    var dpResult = armClient.Deployments.CreateOrUpdate(resourceGroupName, deploymentName, deployment);
                    this.deploymentOutput = (JObject)dpResult.Properties.Outputs;

                    // make test case copy paste easy
                    Console.WriteLine(deploymentOutput.ToString(Newtonsoft.Json.Formatting.Indented).Replace("\"", "\"\""));
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

            return new DecisionServiceDeployment(this.deploymentOutput);
        }

        public void DeleteExistingResourceGroupsMatchingPrefix()
        {
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
