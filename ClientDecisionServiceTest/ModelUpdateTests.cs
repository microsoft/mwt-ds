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

            using (var ds = DecisionService.WithPolicy<TestRcv1Context>(dsConfig).WithEpsilonGreedy(.5f, numActions).ExploitUntilModelReady(new TestRcv1ContextPolicy()))
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