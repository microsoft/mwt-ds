using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClientDecisionService;
using MultiWorldTesting;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;
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
        public void TestInvalidPathOutputDir()
        {
            commandCenter.Reset();
            joinServer.Reset();

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: 2))
            {
                AuthorizationToken = authToken
            };

            dsConfig.CommandCenterAddress = this.commandCenterAddress;
            dsConfig.LoggingServiceAddress = this.joinServerAddress;
            dsConfig.BlobOutputDir = @"c:\";
            dsConfig.PollingForSettingsPeriod = TimeSpan.FromMilliseconds(500);

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

        [TestMethod]
        public void TestUnauthorizedPathOutputDir()
        {
            commandCenter.Reset();
            joinServer.Reset();

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: 2))
            {
                AuthorizationToken = authToken
            };

            dsConfig.CommandCenterAddress = this.commandCenterAddress;
            dsConfig.LoggingServiceAddress = this.joinServerAddress;
            dsConfig.BlobOutputDir = @"c:\windows";
            dsConfig.PollingForSettingsPeriod = TimeSpan.FromMilliseconds(500);

            var cancelTokenSource = new CancellationTokenSource();
            bool exceptionIsExpected = false;

            dsConfig.SettingsPollFailureCallback = (ex) =>
            {
                if (ex is UnauthorizedAccessException)
                {
                    exceptionIsExpected = true;
                    cancelTokenSource.Cancel();
                }
            };

            var ds = new DecisionService<TestContext>(dsConfig);

            cancelTokenSource.Token.WaitHandle.WaitOne(5000);

            Assert.AreEqual(true, exceptionIsExpected);
        }

        [TestMethod]
        public void TestSettingsBlobOutput()
        {
            commandCenter.Reset();
            joinServer.Reset();

            string settingsPath = ".\\dstestsettings";
            Directory.CreateDirectory(settingsPath);

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: 2))
            {
                AuthorizationToken = authToken
            };

            dsConfig.CommandCenterAddress = this.commandCenterAddress;
            dsConfig.LoggingServiceAddress = this.joinServerAddress;
            dsConfig.BlobOutputDir = settingsPath;
            dsConfig.PollingForSettingsPeriod = TimeSpan.FromMilliseconds(500);

            var ds = new DecisionService<TestContext>(dsConfig);

            string settingsFile = Path.Combine(settingsPath, "settings-" + commandCenter.LocalAzureSettingsBlobName);

            int sleepCount = 20;
            while (true && sleepCount > 0)
            {
                Thread.Sleep(100);
                sleepCount--;

                if (File.Exists(settingsFile))
                {
                    break;
                }
            }

            Assert.AreNotEqual(0, sleepCount);

            while (true)
            {
                try
                {
                    byte[] settingsBytes = File.ReadAllBytes(settingsFile);

                    Assert.IsTrue(Enumerable.SequenceEqual(settingsBytes, commandCenter.GetSettingsBlobContent()));
                    break;
                }
                catch (IOException) { }
            }

            ds.Flush();

            Directory.Delete(settingsPath, true);
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
