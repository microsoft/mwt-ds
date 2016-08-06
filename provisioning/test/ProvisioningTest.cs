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
        [Priority(2)]
        public void ProvisionOnlyTest()
        {
            var deployment = new ProvisioningUtil().Deploy();

            Assert.IsNotNull(deployment.ManagementCenterUrl);
            Assert.IsNotNull(deployment.ManagementPassword);
            Assert.IsNotNull(deployment.OnlineTrainerUrl);
            Assert.IsNotNull(deployment.OnlineTrainerToken);
            Assert.IsNotNull(deployment.WebServiceToken);
            Assert.IsNotNull(deployment.SettingsUrl);
        }

        [TestMethod]
        [TestCategory("End to End")]
        [Priority(2)]
        public async Task EndToEnd_Simple()
        {
            var util = new ProvisioningUtil();
            util.DeleteExistingResourceGroupsMatchingPrefix();
            var deployment = util.Deploy();

            //            var deployment = new DecisionServiceDeployment(@"
            //            {
            //  ""management Center URL"": {
            //    ""type"": ""String"",
            //    ""value"": ""https://mc-l6bzp222nonnslkr2h7lahyo2k.azurewebsites.net""
            //  },
            //  ""management Center Password"": {
            //    ""type"": ""String"",
            //    ""value"": ""t26uhzni7gf3g""
            //  },
            //  ""client Library URL"": {
            //    ""type"": ""String"",
            //    ""value"": ""https://storagel6bzp222nonns.blob.core.windows.net/mwt-settings/client?sv=2015-12-11&sr=b&sig=%2BaC%2FQAQdceavnQzGac7d8NwQDpIqCmPWKrT4sNrIASs%3D&st=2016-08-01T22%3A39%3A42Z&se=2017-08-01T22%3A40%3A42Z&sp=r""
            //  },
            //  ""web Service Token"": {
            //    ""type"": ""String"",
            //    ""value"": ""3pmfcjtevpcns""
            //  },
            //  ""online Trainer Token"": {
            //    ""type"": ""String"",
            //    ""value"": ""5a6cgj6753342""
            //  },
            //  ""online Trainer URL"": {
            //    ""type"": ""String"",
            //    ""value"": ""http://trainer-l6bzp222nonnslkr2h7lahyo2k.cloudapp.net""
            //  }
            //}");

            await new SimplePolicyTestClass().SimplePolicyTest(deployment);
        }

        [TestMethod]
        [TestCategory("End to End")]
        [Priority(2)]
        public async Task EndToEnd_ADF()
        {
            var util = new ProvisioningUtil();
            util.DeleteExistingResourceGroupsMatchingPrefix();
            var deployment = util.Deploy();

            new EndToEndOnlineTrainerTest().E2ERankerStochasticRewards(deployment);
        }
    }
}
