using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Research.DecisionServiceTest
{
    public class DecisionServiceDeployment
    {
        public string ManagementCenterUrl { get; set; }
        public string ManagementPassword { get; set; }
        public string OnlineTrainerUrl { get; set; }
        public string OnlineTrainerToken { get; set; }
        public string WebServiceToken { get; set; }
        public string SettingsUrl { get; set; }

        private JObject deploymentOutput;

        public DecisionServiceDeployment(string deploymentOutput) : this(JObject.Parse(deploymentOutput))
        {
        }

        public DecisionServiceDeployment(JObject deploymentOutput)
        {
            this.deploymentOutput = deploymentOutput;

            this.ManagementCenterUrl = this.GetDeploymentOutput("Management Center URL");
            this.ManagementPassword = this.GetDeploymentOutput("Management Center Password");
            this.OnlineTrainerUrl = this.GetDeploymentOutput("Online Trainer URL");
            this.OnlineTrainerToken = this.GetDeploymentOutput("Online Trainer Token");
            this.WebServiceToken = this.GetDeploymentOutput("Web Service Token");
            this.SettingsUrl = this.GetDeploymentOutput("Client Library URL");
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

        public void ConfigureDecisionService(string trainArguments = null, float? initialExplorationEpsilon = null, bool? isExplorationEnabled = null)
        {
            using (var wc = new WebClient())
            {
                wc.Headers.Add($"auth: {ManagementPassword}");
                var query = new List<string>();
                if (trainArguments != null)
                    query.Add("trainArguments=" + HttpUtility.UrlEncode(trainArguments));
                if (initialExplorationEpsilon != null)
                    query.Add("initialExplorationEpsilon=" + initialExplorationEpsilon);
                if (isExplorationEnabled != null)
                    query.Add("isExplorationEnabled=" + isExplorationEnabled);

                var url = ManagementCenterUrl + "/Automation/UpdateSettings";
                if (query.Count > 0)
                    url += "?" + string.Join("&", query);

                wc.DownloadString(url);
            }

            // validate
            var metaData = ApplicationMetadataUtil.DownloadMetadata<ApplicationClientMetadata>(SettingsUrl);
            if (trainArguments != null)
                Assert.AreEqual(trainArguments, metaData.TrainArguments);

            if (initialExplorationEpsilon != null)
                Assert.AreEqual((float)initialExplorationEpsilon, metaData.InitialExplorationEpsilon);

            if (isExplorationEnabled != null)
                Assert.AreEqual((bool)isExplorationEnabled, metaData.IsExplorationEnabled);
        }

        public void OnlineTrainerWaitForStartup()
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < TimeSpan.FromMinutes(20))
            {
                try
                {
                    using (var wc = new WebClient())
                    {
                        wc.DownloadString($"{OnlineTrainerUrl}/status");
                    }
                    return;
                }
                catch (Exception)
                {
                }

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }

        public void OnlineTrainerReset()
        {
            using (var wc = new WebClient())
            {
                try
                {
                    wc.Headers.Add($"Authorization: {OnlineTrainerToken}");
                    wc.DownloadString($"{OnlineTrainerUrl}/reset");
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
    }
}
