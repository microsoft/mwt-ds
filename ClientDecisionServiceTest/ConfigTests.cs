using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class ConfigTests : MockCommandTestBase
    {
        [TestMethod]
        public void TestSingleActionSettingsBlobOutput()
        {
            joinServer.Reset();

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken);

            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;
            dsConfig.PollingForSettingsPeriod = TimeSpan.FromMilliseconds(500);

            using (var ds = DecisionService
                .WithPolicy(dsConfig)
                .With<TestContext>()
                .WithEpsilonGreedy(.2f)
                .ExploitUntilModelReady(new TestSingleActionPolicy()))
            {
                byte[] settingsBytes = null;

                dsConfig.SettingsPollSuccessCallback = data => settingsBytes = data;

                for (int i = 0; i < 20 && settingsBytes == null; i++)
                    Thread.Sleep(100);

                Assert.IsTrue(Enumerable.SequenceEqual(settingsBytes, commandCenter.GetSettingsBlobContent()));
            }
        }

        [TestMethod]
        public void TestMultiActionSettingsBlobOutput()
        {
            joinServer.Reset();

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken);
            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;
            dsConfig.PollingForSettingsPeriod = TimeSpan.FromMilliseconds(500);

            using (var ds = DecisionService
                .WithRanker(dsConfig)
                .With<TestContext>()
                .WithTopSlotEpsilonGreedy(.2f)
                .ExploitUntilModelReady(new TestMultiActionPolicy()))
            {
                byte[] settingsBytes = null;

                dsConfig.SettingsPollSuccessCallback = data => settingsBytes = data;

                for (int i = 0; i < 20 && settingsBytes == null; i++)
                    Thread.Sleep(100);

                Assert.IsTrue(Enumerable.SequenceEqual(settingsBytes, commandCenter.GetSettingsBlobContent()));
            }
        }
    }
}