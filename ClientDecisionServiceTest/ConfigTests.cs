using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class ConfigTests
    {
        [TestMethod]
        public void TestSingleActionInvalidPathOutputDir()
        {
            joinServer.Reset();

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction.DecisionServiceConfiguration<TestContext>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction.EpsilonGreedyExplorer<TestContext>(new TestSingleActionPolicy(), epsilon: 0.2f, numActions: 2));

            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;
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

            var ds = new Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction.DecisionService<TestContext>(dsConfig);

            cancelTokenSource.Token.WaitHandle.WaitOne(5000);

            Assert.AreEqual(true, exceptionIsExpected);
        }

        [TestMethod]
        public void TestSingleActionSettingsBlobOutput()
        {
            joinServer.Reset();

            string settingsPath = ".\\dstestsettings";
            Directory.CreateDirectory(settingsPath);

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction.DecisionServiceConfiguration<TestContext>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction.EpsilonGreedyExplorer<TestContext>(new TestSingleActionPolicy(), epsilon: 0.2f, numActions: 2));

            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;
            dsConfig.BlobOutputDir = settingsPath;
            dsConfig.PollingForSettingsPeriod = TimeSpan.FromMilliseconds(500);

            var ds = new Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction.DecisionService<TestContext>(dsConfig);

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

        [TestMethod]
        public void TestMultiActionInvalidPathOutputDir()
        {
            joinServer.Reset();

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction.DecisionServiceConfiguration<TestContext, DummyADFType>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction.EpsilonGreedyExplorer<TestContext>(new TestMultiActionPolicy(), epsilon: 0.2f, numActions: 2));

            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;
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

            var ds = new Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction.DecisionService<TestContext, DummyADFType>(dsConfig);

            cancelTokenSource.Token.WaitHandle.WaitOne(5000);

            Assert.AreEqual(true, exceptionIsExpected);
        }

        [TestMethod]
        public void TestMultiActionSettingsBlobOutput()
        {
            joinServer.Reset();

            string settingsPath = ".\\dstestsettings";
            Directory.CreateDirectory(settingsPath);

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction.DecisionServiceConfiguration<TestContext, DummyADFType>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction.EpsilonGreedyExplorer<TestContext>(new TestMultiActionPolicy(), epsilon: 0.2f, numActions: 2));

            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;
            dsConfig.BlobOutputDir = settingsPath;
            dsConfig.PollingForSettingsPeriod = TimeSpan.FromMilliseconds(500);

            var ds = new Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction.DecisionService<TestContext, DummyADFType>(dsConfig);

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
            commandCenter = new MockCommandCenter(MockCommandCenter.AuthorizationToken);
            joinServer = new MockJoinServer(MockJoinServer.MockJoinServerAddress);

            joinServer.Run();
        }

        [TestCleanup]
        public void CleanUp()
        {
            joinServer.Stop();
        }

        private MockJoinServer joinServer;
        private MockCommandCenter commandCenter;
    }
}
