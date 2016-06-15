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

namespace Microsoft.Research.DecisionService.Test
{
    [TestClass]
    public class ProvisioningTest
    {
        [TestMethod]
        public async Task TestProvisioning()
        {
            string clientId = ConfigurationManager.AppSettings["ClientId"];
            string passwd = ConfigurationManager.AppSettings["Password"];
            string tenantId = ConfigurationManager.AppSettings["TenantId"];
            string subscriptionId = ConfigurationManager.AppSettings["SubscriptionId"];
            string prefix = ConfigurationManager.AppSettings["prefix"];

            var resourceGroupName = prefix + Guid.NewGuid().ToString().Replace("-","").Substring(4);
            var deploymentName = resourceGroupName + "_deployment";
            var location = "eastus";

            var clientCredentials = new ClientCredential(clientId, passwd);
            var authorityUri = "https://login.windows.net/" + tenantId;
            var context = new AuthenticationContext(authorityUri);
            var authResult = await context.AcquireTokenAsync("https://management.azure.com/", clientCredentials);
            var tokenCredentials = new TokenCloudCredentials(subscriptionId, authResult.AccessToken);
            var credentialsForRM = new TokenCredentials(tokenCredentials.Token);
            var armClient = new ResourceManagementClient(credentialsForRM);

            Trace.WriteLine(resourceGroupName);

            armClient.SubscriptionId = subscriptionId;

            // DELETE OLD
            var oldResources = (await armClient.ResourceGroups.ListAsync())
                .Where(r => r.Name.StartsWith(prefix));

            foreach (var resource in oldResources)
            {
                var task = armClient.ResourceGroups.DeleteAsync(resource.Name);
            }

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
                            Trace.WriteLine($"Status: {op.Properties.TargetResource.ResourceName,-30} '{op.Properties.ProvisioningState}'");
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
                    TemplateLink = new TemplateLink("https://raw.githubusercontent.com/multiworldtesting/ds-provisioning/master/azuredeploy.json"),
                    Parameters = JObject.Parse("{\"numberOfActions\":{\"value\":0}}")
                };

                try
                {
                    var dpResult = await armClient.Deployments.CreateOrUpdateAsync(resourceGroupName, deploymentName, deployment);
                    var outputs = (JObject)dpResult.Properties.Outputs;

                    Trace.WriteLine(outputs);
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

            Assert.IsNull(failedOperation, $"Deployment operation failed: '{failedOperation.Properties.TargetResource.ResourceName}'");
        }
    }
}
