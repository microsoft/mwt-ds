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
using System.Diagnostics;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class ConfigTests
    {
        [TestMethod]
        public void TestInvalidOutputDir()
        {
            commandCenter.Reset();
            joinServer.Reset();

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                authorizationToken: authToken,
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: 2));

            dsConfig.CommandCenterAddress = this.commandCenterAddress;
            dsConfig.LoggingServiceAddress = this.joinServerAddress;
            dsConfig.BlobOutputDir = @"c:\";
            dsConfig.PollingPeriod = TimeSpan.FromMilliseconds(500);

            var cancelTokenSource = new CancellationTokenSource();
            bool exceptionIsExpected = false;

            dsConfig.SettingsPollFailureCallback = (ex) => 
            {
                if (ex is ArgumentNullException && ((ArgumentNullException)ex).ParamName == "path")
                {
                    exceptionIsExpected = true;
                    cancelTokenSource.Cancel();
                }
            };

            var ds = new DecisionService<TestContext>(dsConfig);

            cancelTokenSource.Token.WaitHandle.WaitOne(5000);

            Assert.AreEqual(true, exceptionIsExpected);
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
