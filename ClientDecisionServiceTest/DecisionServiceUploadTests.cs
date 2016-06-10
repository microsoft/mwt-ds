using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class DecisionServiceUploadTests : MockCommandTestBase
    {
        [TestMethod]
        public void TestSingleActionDSUploadSingleEvent()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri);

            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;

            int chosenAction;
            using (var ds = DecisionService
                .Create<TestContext>(dsConfig)
                .ExploitUntilModelReady(new ConstantPolicy<TestContext>()))
            {
                chosenAction = ds.ChooseAction(uniqueKey, new TestContext());
            }

            Assert.AreEqual(1, joinServer.RequestCount);
            Assert.AreEqual(1, joinServer.EventBatchList.Count);
            Assert.AreEqual(1, joinServer.EventBatchList[0].ExperimentalUnitFragments.Count);
            Assert.AreEqual(uniqueKey, joinServer.EventBatchList[0].ExperimentalUnitFragments[0].Id);
            Assert.IsTrue(joinServer.EventBatchList[0].ExperimentalUnitFragments[0].Value.ToLower().Contains("\"a\":" + chosenAction + ","));
        }

        [TestMethod]
        public void TestSingleActionDSUploadMultipleEvents()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri);

            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;

            using (var ds = DecisionService
                .Create<TestContext>(dsConfig)
                .ExploitUntilModelReady(new ConstantPolicy<TestContext>()))
            {

                int chosenAction1 = ds.ChooseAction(uniqueKey, new TestContext());
                int chosenAction2 = ds.ChooseAction(uniqueKey, new TestContext());
                ds.ReportReward(1.0f, uniqueKey);
                ds.ReportOutcome(JsonConvert.SerializeObject(new { value = "test outcome" }), uniqueKey);
            }

            Assert.AreEqual(4, joinServer.EventBatchList.Sum(batch => batch.ExperimentalUnitFragments.Count));
        }

        [TestMethod]
        public void TestSingleActionDSThreadSafeUpload()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var createObservation = (Func<int, string>)((i) => { return string.Format("00000", i); });

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri);
            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;

            int numEvents = 1000;
            var chosenActions = new ConcurrentBag<int>();
            using (var ds = DecisionService
                .Create<TestContext>(dsConfig)
                .WithEpsilonGreedy(.2f)
                .ExploitUntilModelReady(new ConstantPolicy<TestContext>()))
            {
                Parallel.For(0, numEvents, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, (i) =>
                {
                    chosenActions.Add(ds.ChooseAction(uniqueKey, new TestContext()));
                    ds.ReportOutcome(new { value = createObservation((int)i) }, uniqueKey);
                });
            }

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
                .Select(f => JsonConvert.DeserializeObject<SingleActionCompleteExperimentalUnitFragment>(f.Value)));

            // Test actual interactions received 
            List<SingleActionCompleteExperimentalUnitFragment> interactions = completeFragments
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
            List<SingleActionCompleteExperimentalUnitFragment> observations = completeFragments
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

        [TestMethod]
        public void TestMultiActionDSUploadSingleEvent()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri);

            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;

            int[] chosenActions;
            using (var ds = DecisionService.Create<TestContext>(dsConfig)
                //.WithTopSlotEpsilonGreedy(.2f)
                .ExploitUntilModelReady(new ConstantPolicy<TestContext>()))
            {
                chosenActions = ds.ChooseRanking(uniqueKey, new TestContext());
            }

            Assert.AreEqual(1, joinServer.RequestCount);
            Assert.AreEqual(1, joinServer.EventBatchList.Count);
            Assert.AreEqual(1, joinServer.EventBatchList[0].ExperimentalUnitFragments.Count);
            Assert.AreEqual(uniqueKey, joinServer.EventBatchList[0].ExperimentalUnitFragments[0].Id);
            Assert.IsTrue(joinServer.EventBatchList[0].ExperimentalUnitFragments[0].Value.ToLower().Contains("\"a\":[" + string.Join(",", chosenActions) + "],"));
        }

        [TestMethod]
        public void TestMultiActionDSUploadMultipleEvents()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri);
            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;

            using (var ds = DecisionService.Create<TestContext>(dsConfig)
                //.WithTopSlotEpsilonGreedy(.2f)
                .ExploitUntilModelReady(new ConstantPolicy<TestContext>()))
            {
                int[] chosenAction1 = ds.ChooseRanking(uniqueKey, new TestContext());
                int[] chosenAction2 = ds.ChooseRanking(uniqueKey, new TestContext());
                ds.ReportReward(1.0f, uniqueKey);
                ds.ReportOutcome(new { value = "test outcome" }, uniqueKey);
            }
            Assert.AreEqual(4, joinServer.EventBatchList.Sum(batch => batch.ExperimentalUnitFragments.Count));
        }

        [TestMethod]
        public void TestMultiActionDSUploadSelective()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri);
            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;
            dsConfig.InteractionUploadConfiguration = new BatchingConfiguration();
            dsConfig.InteractionUploadConfiguration.MaxDuration = TimeSpan.FromMinutes(10); // allow enough time for queue to buffer events
            dsConfig.InteractionUploadConfiguration.MaxDegreeOfSerializationParallelism = 1; // single-threaded for easy verification

            int numEvents = 100;

            // Set queue capacity to same number of events so selective dropping starts at 50% full
            dsConfig.InteractionUploadConfiguration.MaxUploadQueueCapacity = numEvents;
            dsConfig.InteractionUploadConfiguration.DroppingPolicy = new DroppingPolicy
            {
                MaxQueueLevelBeforeDrop = .5f,

                // when threshold is reached, drop half of the events
                ProbabilityOfDrop = .5f
            };

            using (var ds = DecisionService.Create<TestContext>(dsConfig)
                //.WithTopSlotEpsilonGreedy(.2f)
                .ExploitUntilModelReady(new ConstantPolicy<TestContext>()))
            {
                for (int i = 0; i < numEvents; i++)
                {
                    int[] chosenAction1 = ds.ChooseRanking(uniqueKey, new TestContext());
                }
            }
            // Some events must have been dropped so the total count cannot be same as original
            Assert.IsTrue(joinServer.EventBatchList.Sum(batch => batch.ExperimentalUnitFragments.Count) < numEvents);

            // Get number of events that have been downsampled, i.e. selected with probability q
            int numSampledEvents = joinServer.EventBatchList
                .SelectMany(batch => batch.ExperimentalUnitFragments)
                .Where(e => e.Value.Contains("\"pdrop\":0.5"))
                .Count();

            Assert.IsTrue(numSampledEvents > 0);

            // half of the events are selected with probability 0.5, so this should definitely be less than half the total events
            Assert.IsTrue(numSampledEvents < numEvents / 2);
        }

        [TestMethod]
        public void TestMultiActionDSThreadSafeUpload()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var createObservation = (Func<int, string>)((i) => { return string.Format("00000", i); });

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri);
            dsConfig.InteractionUploadConfiguration = new Microsoft.Research.MultiWorldTesting.JoinUploader.BatchingConfiguration
            {
                MaxBufferSizeInBytes = 4 * 1024 * 1024,
                MaxDuration = TimeSpan.FromMinutes(1),
                MaxEventCount = 10000,
                MaxUploadQueueCapacity = 1024 * 32,
                UploadRetryPolicy = Microsoft.Research.MultiWorldTesting.JoinUploader.BatchUploadRetryPolicy.ExponentialRetry,
                MaxDegreeOfSerializationParallelism = Environment.ProcessorCount
            };

            dsConfig.JoinServerType = JoinServerType.CustomSolution;
            dsConfig.LoggingServiceAddress = MockJoinServer.MockJoinServerAddress;

            int numEvents = 1000;
            var chosenActions = new ConcurrentBag<int[]>();
            using (var ds = DecisionService.Create<TestContext>(dsConfig)
                //.WithTopSlotEpsilonGreedy(.2f)
                .ExploitUntilModelReady(new ConstantPolicy<TestContext>()))
            {
                Parallel.For(0, numEvents, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, (i) =>
                {
                    chosenActions.Add(ds.ChooseRanking(uniqueKey, new TestContext()));
                    ds.ReportOutcome(new { value = createObservation((int)i) }, uniqueKey);
                });
            }
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
                .Select(f => JsonConvert.DeserializeObject<MultiActionCompleteExperimentalUnitFragment>(f.Value)));

            // Test actual interactions received 
            List<MultiActionCompleteExperimentalUnitFragment> interactions = completeFragments
                .Where(f => f.Value == null)
                .OrderBy(f => f.Actions[0])
                .ToList();

            // Test values of the interactions
            Assert.AreEqual(numEvents, interactions.Count);
            var chosenActionList = chosenActions.OrderBy(a => a[0]).ToList();
            for (int i = 0; i < interactions.Count; i++)
            {
                Assert.AreEqual((int)chosenActionList[i][0], interactions[i].Actions[0]);
            }

            // Test actual observations received
            List<MultiActionCompleteExperimentalUnitFragment> observations = completeFragments
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
    }
}