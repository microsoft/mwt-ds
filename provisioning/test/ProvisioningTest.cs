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
        public async Task AllEndToEndTests()
        {
            var util = new ProvisioningUtil();
            util.DeleteExistingResourceGroupsMatchingPrefix();
            var deployment = util.Deploy();

//            var deployment = new DecisionServiceDeployment(@"
//{
//  ""management Center URL"": {
//    ""type"": ""String"",
//    ""value"": ""https://mc-4hpayb6zim2wq5halapiweb5hi.azurewebsites.net""
//  },
//  ""management Center Password"": {
//    ""type"": ""String"",
//    ""value"": ""2w7yf5bg6rr3i""
//  },
//  ""client Library URL"": {
//    ""type"": ""String"",
//    ""value"": ""https://storage4hpayb6zim2wq.blob.core.windows.net/mwt-settings/client?sv=2015-12-11&sr=b&sig=gYdEzTNtNae84zgSQ8ilj40JWZ5HD0NaDZorF1RCK1I%3D&st=2016-07-21T13%3A23%3A24Z&se=2017-07-21T13%3A24%3A24Z&sp=r""
//  },
//  ""web Service Token"": {
//    ""type"": ""String"",
//    ""value"": ""3fgum25ihcnz4""
//  },
//  ""online Trainer Token"": {
//    ""type"": ""String"",
//    ""value"": ""q47zpo4kz76xc""
//  },
//  ""online Trainer URL"": {
//    ""type"": ""String"",
//    ""value"": ""http://trainer-4hpayb6zim2wq5halapiweb5hi.cloudapp.net""
//  }
//}
//");

            await new SimplePolicyTestClass().SimplePolicyTest(deployment);

            deployment.OnlineTrainerReset();

            new EndToEndOnlineTrainerTest().E2ERankerStochasticRewards(deployment);
        }
    }
}
