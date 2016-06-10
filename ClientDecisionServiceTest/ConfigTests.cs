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

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri);

            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;
            dsConfig.PollingForSettingsPeriod = TimeSpan.FromMilliseconds(500);

            using (var ds = DecisionService
                .Create<TestContext>(dsConfig)
                // TODO: update settinsg blob with .WithEpsilonGreedy(.2f)
                .ExploitUntilModelReady(new ConstantPolicy<TestContext>()))
            {
                byte[] settingsBytes = null;

                dsConfig.SettingsPollSuccessCallback = data => settingsBytes = data;

                for (int i = 0; i < 20 && settingsBytes == null; i++)
                {
                    Thread.Sleep(100);
                    if (i % 2 == 0)
                    {
                        // change the settings blob's etag frequently to make sure polling detects it
                        commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false, vwArgs: "abc", initialExplorationEpsilon: 0.3f);
                    }
                }

                Assert.IsTrue(Enumerable.SequenceEqual(settingsBytes, commandCenter.GetSettingsBlobContent("abc", 0.3f)));
            }
        }

        [TestMethod]
        public void TestSettingsBlobPolling()
        {
            joinServer.Reset();

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri);
            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;
            dsConfig.PollingForSettingsPeriod = TimeSpan.FromMilliseconds(500);

            using (var ds = DecisionService
                .Create<TestContext>(dsConfig)
                // TODO .WithTopSlotEpsilonGreedy(.2f)
                .ExploitUntilModelReady(new ConstantPolicy<TestContext>()))
            {
                byte[] settingsBytes = null;

                dsConfig.SettingsPollSuccessCallback = data => settingsBytes = data;

                for (int i = 0; i < 50 && settingsBytes == null; i++)
                {
                    Thread.Sleep(100);
                    if (i % 2 == 0)
                    {
                        // change the settings blob's etag frequently to make sure polling detects it
                        commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false, vwArgs:"abc", initialExplorationEpsilon: 0.4f);
                    }
                }

                Assert.IsNotNull(settingsBytes);
                Assert.IsTrue(Enumerable.SequenceEqual(settingsBytes, commandCenter.GetSettingsBlobContent("abc", 0.4f)));
            }
        }
    }
}