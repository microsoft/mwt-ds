using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class ModularTests
    {
        [TestMethod]
        public void TestSingleActionOfflineModeArgument()
        {
            var dsConfig = new DecisionServiceConfiguration("my token") { OfflineMode = true };
            try
            {
                var policy = VWPolicy.StartWithPolicy(dsConfig, new TestSingleActionPolicy());
                using (var ds = DecisionServiceClient.Create(policy.WithEpsilonGreedy(epsilon: 0.2f, numActionsVariable: 2)))
                { }
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Recorder", ex.ParamName);
            }
        }

        [TestMethod]
        public void TestSingleActionOfflineModeCustomLogger()
        {
            var dsConfig = new DecisionServiceConfiguration("my token") { OfflineMode = true };

            var recorder = new TestLogger();
            int numChooseAction = 100;
            var policy = VWPolicy.StartWithPolicy(dsConfig, new TestSingleActionPolicy());
            using (var ds = DecisionServiceClient.Create(policy.WithEpsilonGreedy(epsilon: 0.2f, numActionsVariable: Constants.NumberOfActions), recorder))
            {
                for (int i = 0; i < numChooseAction; i++)
                {
                    ds.ChooseAction(new UniqueEventID { Key = i.ToString() }, new TestContext());
                }

                Assert.AreEqual(numChooseAction, recorder.NumRecord);

                int numReward = 200;
                for (int i = 0; i < numReward; i++)
                {
                    ds.ReportReward(i, new UniqueEventID { Key = i.ToString() });
                }

                Assert.AreEqual(numReward, recorder.NumReward);

                int numOutcome = 300;
                for (int i = 0; i < numOutcome; i++)
                {
                    ds.ReportOutcome(i.ToString(), new UniqueEventID { Key = i.ToString() });
                }

                Assert.AreEqual(numOutcome, recorder.NumOutcome);
            }

            Assert.AreEqual(0, recorder.NumRecord);
            Assert.AreEqual(0, recorder.NumReward);
            Assert.AreEqual(0, recorder.NumOutcome);
        }

        [TestMethod]
        public void TestSingleActionOnlineModeCustomLogger()
        {
            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken);
            var recorder = new TestLogger();

            int numChooseAction = 100;
            var policy = VWPolicy.StartWithPolicy(dsConfig, new TestSingleActionPolicy());
            using (var ds = DecisionServiceClient.Create(policy.WithEpsilonGreedy(epsilon: .2f, numActionsVariable: Constants.NumberOfActions), recorder))
            {
                for (int i = 0; i < numChooseAction; i++)
                {
                    ds.ChooseAction(new UniqueEventID { Key = i.ToString() }, new TestContext());
                }

                Assert.AreEqual(numChooseAction, recorder.NumRecord);

                int numReward = 200;
                for (int i = 0; i < numReward; i++)
                {
                    ds.ReportReward(i, new UniqueEventID { Key = i.ToString() });
                }

                Assert.AreEqual(numReward, recorder.NumReward);

                int numOutcome = 300;
                for (int i = 0; i < numOutcome; i++)
                {
                    ds.ReportOutcome(i.ToString(), new UniqueEventID { Key = i.ToString() });
                }

                Assert.AreEqual(numOutcome, recorder.NumOutcome);
            }

            Assert.AreEqual(0, recorder.NumRecord);
            Assert.AreEqual(0, recorder.NumReward);
            Assert.AreEqual(0, recorder.NumOutcome);
        }

        [TestMethod]
        public void TestSingleActionOnlineModeInvalidToken()
        {
            MockCommandCenter.UnsetRedirectionBlobLocation();

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken);
            dsConfig.JoinServerType = JoinServerType.CustomSolution;

            bool isExceptionExpected = false;
            try
            {
                var policy = VWPolicy.StartWithPolicy(dsConfig, new TestSingleActionPolicy());
                using (var ds = DecisionServiceClient.Create(policy.WithEpsilonGreedy(epsilon: .2f, numActionsVariable: Constants.NumberOfActions)))
                { }
            }
            catch (InvalidDataException)
            {
                isExceptionExpected = true;
            }
            Assert.AreEqual(true, isExceptionExpected);
        }

        [TestMethod]
        public void TestMultiActionOfflineModeArgument()
        {
            var dsConfig = new DecisionServiceConfiguration("my token") { OfflineMode = true };
            try
            {
                var ranker = VWPolicy.StartWithRanker(dsConfig, new TestMultiActionPolicy());
                using (var ds = DecisionServiceClient.Create(ranker.WithTopSlotEpsilonGreedy(epsilon: .2f, numActionsVariable: 2)))
                { }
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Recorder", ex.ParamName);
            }
        }

        [TestMethod]
        public void TestMultiActionOfflineModeCustomLogger()
        {
            var dsConfig = new DecisionServiceConfiguration("my token") { OfflineMode = true };
            var recorder = new TestLogger();
            int numChooseAction = 100;
            var ranker = VWPolicy.StartWithRanker(dsConfig, new TestMultiActionPolicy());
            using (var ds = DecisionServiceClient.Create(ranker.WithTopSlotEpsilonGreedy(epsilon: .2f, numActionsVariable: Constants.NumberOfActions), recorder))
            {
                for (int i = 0; i < numChooseAction; i++)
                {
                    ds.ChooseAction(new UniqueEventID { Key = i.ToString() }, new TestContext());
                }

                Assert.AreEqual(numChooseAction, recorder.NumRecord);

                int numReward = 200;
                for (int i = 0; i < numReward; i++)
                {
                    ds.ReportReward(i, new UniqueEventID { Key = i.ToString() });
                }

                Assert.AreEqual(numReward, recorder.NumReward);

                int numOutcome = 300;
                for (int i = 0; i < numOutcome; i++)
                {
                    ds.ReportOutcome(i.ToString(), new UniqueEventID { Key = i.ToString() });
                }

                Assert.AreEqual(numOutcome, recorder.NumOutcome);
            }
            Assert.AreEqual(0, recorder.NumRecord);
            Assert.AreEqual(0, recorder.NumReward);
            Assert.AreEqual(0, recorder.NumOutcome);
        }

        [TestMethod]
        public void TestMultiActionOnlineModeCustomLogger()
        {
            joinServer.Reset();

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken);

            var recorder = new TestLogger();
            dsConfig.PollingForModelPeriod = TimeSpan.MinValue;
            dsConfig.PollingForSettingsPeriod = TimeSpan.MinValue;
            dsConfig.JoinServerType = JoinServerType.CustomSolution;

            int numChooseAction = 100;
            var policy = VWPolicy.StartWithRanker(dsConfig, new TestMultiActionPolicy());
            using (var ds = DecisionServiceClient.Create(policy.WithTopSlotEpsilonGreedy(epsilon: .2f, numActionsVariable: Constants.NumberOfActions), recorder))
            {
                for (int i = 0; i < numChooseAction; i++)
                {
                    ds.ChooseAction(new UniqueEventID { Key = i.ToString() }, new TestContext());
                }

                Assert.AreEqual(numChooseAction, recorder.NumRecord);

                int numReward = 200;
                for (int i = 0; i < numReward; i++)
                {
                    ds.ReportReward(i, new UniqueEventID { Key = i.ToString() });
                }

                Assert.AreEqual(numReward, recorder.NumReward);

                int numOutcome = 300;
                for (int i = 0; i < numOutcome; i++)
                {
                    ds.ReportOutcome(i.ToString(), new UniqueEventID { Key = i.ToString() });
                }

                Assert.AreEqual(numOutcome, recorder.NumOutcome);
            }
            Assert.AreEqual(0, recorder.NumRecord);
            Assert.AreEqual(0, recorder.NumReward);
            Assert.AreEqual(0, recorder.NumOutcome);
        }

        [TestMethod]
        public void TestMultiActionOnlineModeInvalidToken()
        {
            MockCommandCenter.UnsetRedirectionBlobLocation();

            /*** This test requires real value of RedirectionBlobLocation set in DecisionServiceConstants.cs file ***/
            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken);
            dsConfig.JoinServerType = JoinServerType.CustomSolution;

            bool isExceptionExpected = false;
            try
            {
                var policy = VWPolicy.StartWithRanker(dsConfig, new TestMultiActionPolicy());
                using (var ds = DecisionServiceClient.Create(policy.WithTopSlotEpsilonGreedy(epsilon: .2f, numActionsVariable: Constants.NumberOfActions)))
                { }
            }
            catch (InvalidDataException)
            {
                isExceptionExpected = true;
            }
            Assert.AreEqual(true, isExceptionExpected);
        }

        [TestInitialize]
        public void Setup()
        {
            joinServer = new MockJoinServer(MockJoinServer.MockJoinServerAddress);
            joinServer.Run();

            MockCommandCenter.SetRedirectionBlobLocation();
        }

        [TestCleanup]
        public void CleanUp()
        {
            joinServer.Stop();
            MockCommandCenter.UnsetRedirectionBlobLocation();
        }

        private MockJoinServer joinServer;
    }
}