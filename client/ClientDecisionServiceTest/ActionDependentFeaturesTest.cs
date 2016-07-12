using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class ActionDependentFeaturesTest : MockCommandTestBase
    {
        [TestMethod]
        [TestCategory("Client Library")]
        [Priority(1)]
        public void TestADFExplorationResult()
        {
            joinServer.Reset();

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false, vwArgs:"--cb_explore_adf --epsilon 0.5");

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri)
            {
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue,
                JoinServerType = JoinServerType.CustomSolution,
                LoggingServiceAddress = MockJoinServer.MockJoinServerAddress
            };

            using (var ds = DecisionService.Create<TestADFContext>(dsConfig)
                .ExploitUntilModelReady(new ConstantPolicy<TestADFContext>(ctx => ctx.ActionDependentFeatures.Count)))
            {
                string uniqueKey = "eventid";

                for (int i = 1; i <= 100; i++)
                {
                    var adfContext = new TestADFContext(i);
                    int[] action = ds.ChooseRanking(uniqueKey, adfContext);

                    Assert.AreEqual(i, action.Length);

                    // verify all unique actions in the list
                    Assert.AreEqual(action.Length, action.Distinct().Count());

                    // verify the actions are in the expected range
                    Assert.AreEqual((i * (i + 1)) / 2, action.Sum(a => a));

                    ds.ReportReward(i / 100f, uniqueKey);
                }
            }
            Assert.AreEqual(200, joinServer.EventBatchList.Sum(b => b.ExperimentalUnitFragments.Count));
        }

        [TestMethod]
        [TestCategory("Client Library")]
        [Priority(1)]
        public void TestADFModelUpdateFromStream()
        {
            joinServer.Reset();

            string vwArgs = "--cb_explore_adf --epsilon 0.5";
            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false, vwArgs: vwArgs);

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri)
            {
                JoinServerType = JoinServerType.CustomSolution,
                LoggingServiceAddress = MockJoinServer.MockJoinServerAddress,
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue
            };

            using (var ds = DecisionService.Create<TestADFContextWithFeatures>(dsConfig)
                .ExploitUntilModelReady(new ConstantPolicy<TestADFContextWithFeatures>(ctx => ctx.ActionDependentFeatures.Count)))
            {
                string uniqueKey = "eventid";

                for (int i = 1; i <= 100; i++)
                {
                    Random rg = new Random(i);

                    if (i % 50 == 1)
                    {
                        int modelIndex = i / 50;
                        byte[] modelContent = commandCenter.GetCBADFModelBlobContent(numExamples: 3 + modelIndex, numFeatureVectors: 4 + modelIndex, vwDefaultArgs: vwArgs);
                        using (var modelStream = new MemoryStream(modelContent))
                        {
                            ds.UpdateModel(modelStream);
                        }
                    }

                    int numActions = rg.Next(5, 20);
                    var context = TestADFContextWithFeatures.CreateRandom(numActions, rg);

                    int[] action = ds.ChooseRanking(uniqueKey, context);

                    Assert.AreEqual(numActions, action.Length);

                    // verify all unique actions in the list
                    Assert.AreEqual(action.Length, action.Distinct().Count());

                    // verify the actions are in the expected range
                    Assert.AreEqual((numActions * (numActions + 1)) / 2, action.Sum(a => a));

                    ds.ReportReward(i / 100f, uniqueKey);
                }
            }
            Assert.AreEqual(200, joinServer.EventBatchList.Sum(b => b.ExperimentalUnitFragments.Count));
        }
    }
}