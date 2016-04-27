using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class ModelUpdate : MockCommandTestBase
    {
        [TestMethod]
        public void TestRcv1ModelUpdateFromStream()
        {
            joinServer.Reset();

            int numActions = 10;
            int numFeatures = 1024;

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken)
            //explorer: new EpsilonGreedyExplorer<TestRcv1Context>(new TestRcv1ContextPolicy(), epsilon: 0.5f, numActions: (int)numActions))
            {
                JoinServerType = JoinServerType.CustomSolution,
                LoggingServiceAddress = MockJoinServer.MockJoinServerAddress,
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue
            };

            using (var ds = DecisionService
                .WithPolicy(dsConfig, numActions)
                .With<TestRcv1Context>()
                .WithEpsilonGreedy(.5f)
                .ExploitUntilModelReady(new TestRcv1ContextPolicy()))
            {
                string uniqueKey = "eventid";

                for (int i = 1; i <= 100; i++)
                {
                    Random rg = new Random(i);

                    if (i % 50 == 1)
                    {
                        int modelIndex = i / 50;
                        byte[] modelContent = commandCenter.GetCBModelBlobContent(numExamples: 3 + modelIndex, numFeatures: numFeatures, numActions: numActions);
                        using (var modelStream = new MemoryStream(modelContent))
                        {
                            ds.UpdateModel(modelStream);
                        }
                    }

                    var context = TestRcv1Context.CreateRandom(numActions, numFeatures, rg);

                    DateTime timeStamp = DateTime.UtcNow;

                    int action = ds.ChooseAction(new UniqueEventID { Key = uniqueKey }, context);

                    // verify the actions are in the expected range
                    Assert.IsTrue(action >= 1 && action <= numActions);

                    ds.ReportReward(i / 100f, new UniqueEventID { Key = uniqueKey });
                }
            }

            Assert.AreEqual(200, joinServer.EventBatchList.Sum(b => b.ExperimentalUnitFragments.Count));
        }

        [TestMethod]
        [ExpectedException(typeof(StorageException))]
        public async Task TestNoModelFoundForImmediateDownload()
        {
            // create mock blobs for settings and models
            this.commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false);

            var serviceConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken)
            {
                JoinServerType = JoinServerType.CustomSolution,
                LoggingServiceAddress = MockJoinServer.MockJoinServerAddress,
            };

            using (var service = DecisionService
                .WithRanker(serviceConfig)
                .With<TestADFContextWithFeatures, TestADFFeatures>(context => context.ActionDependentFeatures)
                .WithTopSlotEpsilonGreedy(epsilon: .2f))
            {
                await service.DownloadModelAndUpdate(new System.Threading.CancellationToken());
            }
        }

        [TestMethod]
        public void TestModelImmediateDownload()
        {
            // create mock blobs for settings and models
            this.commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: true);

            var serviceConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken)
            {
                JoinServerType = JoinServerType.CustomSolution,
                LoggingServiceAddress = MockJoinServer.MockJoinServerAddress,
            };

            using (var service = DecisionService
                .WithRanker(serviceConfig)
                .With<TestADFContextWithFeatures, TestADFFeatures>(context => context.ActionDependentFeatures)
                .WithTopSlotEpsilonGreedy(epsilon: .2f))
            {
                // download model right away
                service.DownloadModelAndUpdate(new System.Threading.CancellationToken()).Wait();
            }
            // No exception if everything works
        }
    }
}