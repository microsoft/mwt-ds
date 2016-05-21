using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;
using VW.Serializer;

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
                using (var ds = DecisionService.WithPolicy(dsConfig, 2).With<TestContext>())
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
            using (var ds = DecisionService
                .WithPolicy(dsConfig, Constants.NumberOfActions)
                .With<TestContext>()
                .WithEpsilonGreedy(.2f)
                .WithRecorder(recorder)
                .ExploitUntilModelReady(new TestSingleActionPolicy()))
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
            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.AuthorizationToken)
            {
                JoinServerType = JoinServerType.CustomSolution
            };

            var recorder = new TestLogger();

            int numChooseAction = 100;
            using (var ds = DecisionService
                .WithPolicy(dsConfig, Constants.NumberOfActions)
                .With<TestContext>()
                .WithEpsilonGreedy(.2f)
                .WithRecorder(recorder)
                .ExploitUntilModelReady(new TestSingleActionPolicy()))
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
                using (var ds = DecisionService
                    .WithPolicy(dsConfig, Constants.NumberOfActions)
                    .With<TestContext>()
                    .ExploitUntilModelReady(new TestSingleActionPolicy()))
                { }
            }
            catch (InvalidDataException)
            {
                isExceptionExpected = true;
            }
            Assert.AreEqual(true, isExceptionExpected);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public void TestMultiActionOfflineModeArgument()
        {
            using (var ds = DecisionService
                .WithRanker(new DecisionServiceConfiguration("my token") { OfflineMode = true })
                .WithJson())
            {
                ds.ChooseAction(new UniqueEventID() { Key = "" }, "{}");
            }
        }

        [TestMethod]
        public void TestMultiActionOfflineModeCustomLogger()
        {
            var dsConfig = new DecisionServiceConfiguration("my token") { OfflineMode = true };
            var recorder = new TestLogger();
            int numChooseAction = 100;
            using (var ds = DecisionService
                .WithRanker(dsConfig)
                .With<TestContext>()
                .WithTopSlotEpsilonGreedy(.2f)
                .WithRecorder(recorder)
                .ExploitUntilModelReady(new TestMultiActionPolicy()))
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
            using (var ds = DecisionService
                .WithRanker(dsConfig)
                .With<TestContext>(JsonTypeInspector.Default)
                .WithTopSlotEpsilonGreedy(.2f)
                .WithRecorder(recorder)
                .ExploitUntilModelReady(new TestMultiActionPolicy()))
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
                using (var ds = DecisionService
                    .WithRanker(dsConfig)
                    .With<TestContext>()
                    .WithTopSlotEpsilonGreedy(.2f)
                    .ExploitUntilModelReady(new TestMultiActionPolicy()))
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