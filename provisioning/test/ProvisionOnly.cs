using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.DecisionServiceTest
{
    [TestClass]
    public class ProvisionOnly : ProvisioningBaseTest
    {
        [TestMethod]
        [TestCategory("End to End")]
        [Priority(2)]
        public void ProvisionOnlyTest()
        {
            Assert.IsNotNull(this.managementCenterUrl);
            Assert.IsNotNull(this.managementPassword);
            Assert.IsNotNull(this.onlineTrainerUrl);
            Assert.IsNotNull(this.onlineTrainerToken);
            Assert.IsNotNull(this.webServiceToken);
            Assert.IsNotNull(this.settingsUrl);
        }
    }
}
