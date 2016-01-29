using ClientDecisionService;
using ClientDecisionService.SingleAction;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MultiWorldTesting;
using MultiWorldTesting.SingleAction;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class DecisionServiceUploadTests
    {
        [TestMethod]
        public void TestDSUploadSingleEvent()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;

            var ds = new DecisionService<TestContext>(dsConfig);

            uint chosenAction = ds.ChooseAction(new UniqueEventID { Key = uniqueKey }, new TestContext());

            ds.Flush();

            Assert.AreEqual(1, joinServer.RequestCount);
            Assert.AreEqual(1, joinServer.EventBatchList.Count);
            Assert.AreEqual(1, joinServer.EventBatchList[0].ExperimentalUnitFragments.Count);
            Assert.AreEqual(uniqueKey, joinServer.EventBatchList[0].ExperimentalUnitFragments[0].Id);
            Assert.IsTrue(joinServer.EventBatchList[0].ExperimentalUnitFragments[0].Value.ToLower().Contains("\"a\":" + chosenAction + ","));
        }

        [TestMethod]
        public void TestDSUploadMultipleEvents()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;

            var ds = new DecisionService<TestContext>(dsConfig);

            uint chosenAction1 = ds.ChooseAction(new UniqueEventID { Key = uniqueKey }, new TestContext());
            uint chosenAction2 = ds.ChooseAction(new UniqueEventID { Key = uniqueKey }, new TestContext());
            ds.ReportReward(1.0f, new UniqueEventID { Key = uniqueKey });
            ds.ReportOutcome(JsonConvert.SerializeObject(new { value = "test outcome" }), new UniqueEventID { Key = uniqueKey });

            ds.Flush();

            Assert.AreEqual(4, joinServer.EventBatchList.Sum(batch => batch.ExperimentalUnitFragments.Count));
        }

        [TestMethod]
        public void TestDSThreadSafeUpload()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var createObservation = (Func<int, string>)((i) => { return string.Format("00000", i); });

            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;

            var ds = new DecisionService<TestContext>(dsConfig);

            int numEvents = 1000;

            var chosenActions = new ConcurrentBag<uint>();

            //Parallel.For(0, numEvents, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, (i) =>
            for (int i = 0; i < numEvents; i++)
            {
                chosenActions.Add(ds.ChooseAction(new UniqueEventID { Key = uniqueKey }, new TestContext()));
                ds.ReportOutcome(JsonConvert.SerializeObject(new { value = createObservation(i) }), new UniqueEventID { Key = uniqueKey });
            }
            //);

            ds.Flush();

            List<PartialDecisionServiceMessage> batchList = this.joinServer.EventBatchList;
            int numActualEvents = batchList.Sum(b => b.ExperimentalUnitFragments.Count);
            Assert.AreEqual(numEvents * 2, numActualEvents);

            List<string> uniqueKeys = batchList
                .SelectMany(b => b.ExperimentalUnitFragments.Select(f => f.Id))
                .Distinct()
                .ToList();

            Assert.AreEqual(1, uniqueKeys.Count);
            Assert.AreEqual(uniqueKey, uniqueKeys[0]);

            var completeFragments = batchList
                .SelectMany(b => b.ExperimentalUnitFragments
                .Select(f => JsonConvert.DeserializeObject<CompleteExperimentalUnitFragment>(f.Value)));

            // Test actual interactions received 
            List<CompleteExperimentalUnitFragment> interactions = completeFragments
                .Where(f => f.Value == null)
                .OrderBy(f => f.Action)
                .ToList();

            // Test values of the interactions
            Assert.AreEqual(numEvents, interactions.Count);
            var chosenActionList = chosenActions.OrderBy(a => a).ToList();
            for (int i = 0; i < interactions.Count; i++)
            {
                Assert.AreEqual((int)chosenActionList[i], interactions[i].Action.Value);
            }

            // Test actual observations received
            List<CompleteExperimentalUnitFragment> observations = completeFragments
                .Where(f => f.Value != null)
                .OrderBy(f => f.Value)
                .ToList();

            // Test values of the observations
            Assert.AreEqual(numEvents, observations.Count);
            for (int i = 0; i < observations.Count; i++)
            {
                Assert.AreEqual(JsonConvert.SerializeObject(new { value = createObservation(i) }), observations[i].Value);
            }
        }

        [TestInitialize]
        public void Setup()
        {
            joinServer = new MockJoinServer(MockJoinServer.MockJoinServerAddress);

            joinServer.Run();
        }

        [TestCleanup]
        public void CleanUp()
        {
            joinServer.Stop();
        }

        private MockJoinServer joinServer;
    }
}
