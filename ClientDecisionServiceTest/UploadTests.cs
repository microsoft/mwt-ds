using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClientDecisionService;
using MultiWorldTesting;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Text;
using Microsoft.Research.DecisionService.Common;
using Newtonsoft.Json;
using System.Web;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class UploadTests
    {
        [TestMethod]
        public void TestUpload()
        {
            commandCenter.Reset();
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                authorizationToken: this.authToken,
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));
            dsConfig.LoggingServiceAddress = this.joinServerAddress;
            dsConfig.CommandCenterAddress = this.commandCenterAddress;

            var ds = new DecisionService<TestContext>(dsConfig);

            uint chosenAction = ds.ChooseAction(uniqueKey, new TestContext());

            ds.Flush();

            Assert.AreEqual(1, joinServer.RequestCount);
            Assert.AreEqual(1, joinServer.EventBatchList.Count);
            Assert.AreEqual(1, joinServer.EventBatchList[0].ExperimentalUnitFragments.Count);
            Assert.AreEqual(uniqueKey, joinServer.EventBatchList[0].ExperimentalUnitFragments[0].Id);
            Assert.IsTrue(joinServer.EventBatchList[0].ExperimentalUnitFragments[0].Value.ToLower().Contains("\"a\":" + chosenAction + ","));
        }

        [TestInitialize]
        public void Setup()
        {
            commandCenter = new MockCommandCenter(commandCenterAddress);
            joinServer = new MockJoinServer(joinServerAddress);

            commandCenter.Run();
            joinServer.Run();
        }

        [TestCleanup]
        public void CleanUp()
        {
            commandCenter.Stop();
            joinServer.Stop();
        }

        private readonly string commandCenterAddress = "http://localhost:9090/";
        private readonly string joinServerAddress = "http://localhost:9091/";
        private readonly string authToken = "test token";
        private MockCommandCenter commandCenter;
        private MockJoinServer joinServer;
    }
}
