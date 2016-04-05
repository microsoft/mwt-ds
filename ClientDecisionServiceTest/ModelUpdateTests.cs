using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class ModelUpdate
    {
        [TestMethod]
        public void TestRcv1ModelUpdateFromFile()
        {
            joinServer.Reset();

            int numActions = 10;
            int numFeatures = 1024;

            var dsConfig = new DecisionServiceConfiguration(authorizationToken: MockCommandCenter.AuthorizationToken)
            {
                JoinServerType = JoinServerType.CustomSolution,
                LoggingServiceAddress = MockJoinServer.MockJoinServerAddress,
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue
            };

            var actualModelFiles = new List<string>();
            var policy = VWPolicy.StartWithPolicy(dsConfig, new TestRcv1ContextPolicy());
            using (var ds = DecisionServiceClient.Create(policy.WithEpsilonGreedy(epsilon: 0.5f, numActionsVariable: (uint)numActions)))
            {
                string uniqueKey = "eventid";

                string modelFile = "test_vw_adf{0}.model";

                for (int i = 1; i <= 100; i++)
                {
                    Random rg = new Random(i);

                    if (i % 50 == 1)
                    {
                        int modelIndex = i / 50;
                        string currentModelFile = string.Format(modelFile, modelIndex);

                        byte[] modelContent = commandCenter.GetCBModelBlobContent(numExamples: 3 + modelIndex, numFeatures: numFeatures, numActions: numActions);
                        System.IO.File.WriteAllBytes(currentModelFile, modelContent);

                        ds.UpdateModel(File.OpenRead(currentModelFile));

                        actualModelFiles.Add(currentModelFile);
                    }

                    var context = TestRcv1Context.CreateRandom(numActions, numFeatures, rand: rg);

                    DateTime timeStamp = DateTime.UtcNow;

                    uint action = ds.ChooseAction(new UniqueEventID { Key = uniqueKey }, context);

                    // verify the actions are in the expected range
                    Assert.IsTrue(action >= 1 && action <= numActions);

                    ds.ReportReward(i / 100f, new UniqueEventID { Key = uniqueKey });
                }
            }

            Assert.AreEqual(200, joinServer.EventBatchList.Sum(b => b.ExperimentalUnitFragments.Count));

            foreach (string actualModelFile in actualModelFiles)
            {
                System.IO.File.Delete(actualModelFile);
            }
        }

        [TestMethod]
        public void TestRcv1ModelUpdateFromStream()
        {
            joinServer.Reset();

            int numActions = 10;
            int numFeatures = 1024;

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken)
                //explorer: new EpsilonGreedyExplorer<TestRcv1Context>(new TestRcv1ContextPolicy(), epsilon: 0.5f, numActions: (uint)numActions))
            {
                JoinServerType = JoinServerType.CustomSolution,
                LoggingServiceAddress = MockJoinServer.MockJoinServerAddress,
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue
            };

            var policy = VWPolicy.StartWithPolicy(dsConfig, new TestRcv1ContextPolicy());
            using (var ds = DecisionServiceClient.Create(policy.WithEpsilonGreedy(epsilon: .5f, numActionsVariable: (uint)numActions)))
            {
                string uniqueKey = "eventid";

                string modelFile = "test_vw_adf{0}.model";

                for (int i = 1; i <= 100; i++)
                {
                    Random rg = new Random(i);

                    if (i % 50 == 1)
                    {
                        int modelIndex = i / 50;
                        string currentModelFile = string.Format(modelFile, modelIndex);

                        byte[] modelContent = commandCenter.GetCBModelBlobContent(numExamples: 3 + modelIndex, numFeatures: numFeatures, numActions: numActions);

                        var modelStream = new MemoryStream(modelContent);

                        ds.UpdateModel(modelStream);
                    }

                    var context = TestRcv1Context.CreateRandom(numActions, numFeatures, rg);

                    DateTime timeStamp = DateTime.UtcNow;

                    uint action = ds.ChooseAction(new UniqueEventID { Key = uniqueKey }, context);

                    // verify the actions are in the expected range
                    Assert.IsTrue(action >= 1 && action <= numActions);

                    ds.ReportReward(i / 100f, new UniqueEventID { Key = uniqueKey });
                }
            }

            Assert.AreEqual(200, joinServer.EventBatchList.Sum(b => b.ExperimentalUnitFragments.Count));
        }

        [TestInitialize]
        public void Setup()
        {
            joinServer = new MockJoinServer(MockJoinServer.MockJoinServerAddress);

            joinServer.Run();

            commandCenter = new MockCommandCenter(MockCommandCenter.AuthorizationToken);
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
