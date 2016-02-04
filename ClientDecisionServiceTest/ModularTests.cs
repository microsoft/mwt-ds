using ClientDecisionService;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MultiWorldTesting;
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
            var dsConfig = new ClientDecisionService.SingleAction.DecisionServiceConfiguration<TestContext>(
                authorizationToken: "my token",
                explorer: new MultiWorldTesting.SingleAction.EpsilonGreedyExplorer<TestContext>(new TestSingleActionPolicy(), epsilon: 0.2f, numActions: 2));

            dsConfig.OfflineMode = true;

            try
            {
                var ds = new ClientDecisionService.SingleAction.DecisionService<TestContext>(dsConfig);
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Recorder", ex.ParamName);
            }
        }

        [TestMethod]
        public void TestSingleActionOfflineModeCustomLogger()
        {
            var dsConfig = new ClientDecisionService.SingleAction.DecisionServiceConfiguration<TestContext>(
                authorizationToken: "my token",
                explorer: new MultiWorldTesting.SingleAction.EpsilonGreedyExplorer<TestContext>(new TestSingleActionPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            dsConfig.OfflineMode = true;
            dsConfig.Recorder = new TestLogger();

            int numChooseAction = 100;
            var ds = new ClientDecisionService.SingleAction.DecisionService<TestContext>(dsConfig); 
            for (int i = 0; i < numChooseAction; i++)
            {
                ds.ChooseAction(new UniqueEventID { Key = i.ToString() }, new TestContext());
            }

            Assert.AreEqual(numChooseAction, ((TestLogger)dsConfig.Recorder).NumRecord);

            int numReward = 200;
            for (int i = 0; i < numReward; i++)
            {
                ds.ReportReward(i, new UniqueEventID { Key = i.ToString() });
            }

            Assert.AreEqual(numReward, ((TestLogger)dsConfig.Recorder).NumReward);

            int numOutcome = 300;
            for (int i = 0; i < numOutcome; i++)
            {
                ds.ReportOutcome(i.ToString(), new UniqueEventID { Key = i.ToString() });
            }

            Assert.AreEqual(numOutcome, ((TestLogger)dsConfig.Recorder).NumOutcome);

            ds.Flush();

            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumRecord);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumReward);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumOutcome);
        }

        [TestMethod]
        public void TestSingleActionOnlineModeCustomLogger()
        {
            var dsConfig = new ClientDecisionService.SingleAction.DecisionServiceConfiguration<TestContext>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new MultiWorldTesting.SingleAction.EpsilonGreedyExplorer<TestContext>(new TestSingleActionPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            dsConfig.Recorder = new TestLogger();

            int numChooseAction = 100;
            var ds = new ClientDecisionService.SingleAction.DecisionService<TestContext>(dsConfig);
            for (int i = 0; i < numChooseAction; i++)
            {
                ds.ChooseAction(new UniqueEventID { Key = i.ToString() }, new TestContext());
            }

            Assert.AreEqual(numChooseAction, ((TestLogger)dsConfig.Recorder).NumRecord);

            int numReward = 200;
            for (int i = 0; i < numReward; i++)
            {
                ds.ReportReward(i, new UniqueEventID { Key = i.ToString() });
            }

            Assert.AreEqual(numReward, ((TestLogger)dsConfig.Recorder).NumReward);

            int numOutcome = 300;
            for (int i = 0; i < numOutcome; i++)
            {
                ds.ReportOutcome(i.ToString(), new UniqueEventID { Key = i.ToString() });
            }

            Assert.AreEqual(numOutcome, ((TestLogger)dsConfig.Recorder).NumOutcome);

            ds.Flush();

            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumRecord);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumReward);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumOutcome);
        }

        [TestMethod]
        public void TestSingleActionOnlineModeInvalidToken()
        {
            MockCommandCenter.UnsetRedirectionBlobLocation();

            var dsConfig = new ClientDecisionService.SingleAction.DecisionServiceConfiguration<TestContext>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new MultiWorldTesting.SingleAction.EpsilonGreedyExplorer<TestContext>(new TestSingleActionPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            bool isExceptionExpected = false;
            try
            {
                var ds = new ClientDecisionService.SingleAction.DecisionService<TestContext>(dsConfig);
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
            var dsConfig = new ClientDecisionService.MultiAction.DecisionServiceConfiguration<TestContext, DummyADFType>(
                authorizationToken: "my token",
                explorer: new MultiWorldTesting.MultiAction.EpsilonGreedyExplorer<TestContext>(new TestMultiActionPolicy(), epsilon: 0.2f, numActions: 2));

            dsConfig.OfflineMode = true;

            try
            {
                var ds = new ClientDecisionService.MultiAction.DecisionService<TestContext, DummyADFType>(dsConfig);
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Recorder", ex.ParamName);
            }
        }

        [TestMethod]
        public void TestMultiActionOfflineModeCustomLogger()
        {
            var dsConfig = new ClientDecisionService.MultiAction.DecisionServiceConfiguration<TestContext, DummyADFType>(
                authorizationToken: "my token",
                explorer: new MultiWorldTesting.MultiAction.EpsilonGreedyExplorer<TestContext>(new TestMultiActionPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            dsConfig.OfflineMode = true;
            dsConfig.Recorder = new TestLogger();

            int numChooseAction = 100;
            var ds = new ClientDecisionService.MultiAction.DecisionService<TestContext, DummyADFType>(dsConfig);
            for (int i = 0; i < numChooseAction; i++)
            {
                ds.ChooseAction(new UniqueEventID { Key = i.ToString() }, new TestContext());
            }

            Assert.AreEqual(numChooseAction, ((TestLogger)dsConfig.Recorder).NumRecord);

            int numReward = 200;
            for (int i = 0; i < numReward; i++)
            {
                ds.ReportReward(i, new UniqueEventID { Key = i.ToString() });
            }

            Assert.AreEqual(numReward, ((TestLogger)dsConfig.Recorder).NumReward);

            int numOutcome = 300;
            for (int i = 0; i < numOutcome; i++)
            {
                ds.ReportOutcome(i.ToString(), new UniqueEventID { Key = i.ToString() });
            }

            Assert.AreEqual(numOutcome, ((TestLogger)dsConfig.Recorder).NumOutcome);

            ds.Flush();

            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumRecord);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumReward);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumOutcome);
        }

        [TestMethod]
        public void TestMultiActionOnlineModeCustomLogger()
        {
            var dsConfig = new ClientDecisionService.MultiAction.DecisionServiceConfiguration<TestContext, DummyADFType>
            (
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new MultiWorldTesting.MultiAction.EpsilonGreedyExplorer<TestContext>(new TestMultiActionPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions)
            );

            dsConfig.Recorder = new TestLogger();
            dsConfig.PollingForModelPeriod = TimeSpan.MinValue;
            dsConfig.PollingForSettingsPeriod = TimeSpan.MinValue;

            int numChooseAction = 100;
            var ds = new ClientDecisionService.MultiAction.DecisionService<TestContext, DummyADFType>(dsConfig);
            for (int i = 0; i < numChooseAction; i++)
            {
                ds.ChooseAction(new UniqueEventID { Key = i.ToString() }, new TestContext());
            }

            Assert.AreEqual(numChooseAction, ((TestLogger)dsConfig.Recorder).NumRecord);

            int numReward = 200;
            for (int i = 0; i < numReward; i++)
            {
                ds.ReportReward(i, new UniqueEventID { Key = i.ToString() });
            }

            Assert.AreEqual(numReward, ((TestLogger)dsConfig.Recorder).NumReward);

            int numOutcome = 300;
            for (int i = 0; i < numOutcome; i++)
            {
                ds.ReportOutcome(i.ToString(), new UniqueEventID { Key = i.ToString() });
            }

            Assert.AreEqual(numOutcome, ((TestLogger)dsConfig.Recorder).NumOutcome);

            ds.Flush();

            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumRecord);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumReward);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumOutcome);
        }

        [TestMethod]
        public void TestMultiActionOnlineModeInvalidToken()
        {
            MockCommandCenter.UnsetRedirectionBlobLocation();

            /*** This test requires real value of RedirectionBlobLocation set in DecisionServiceConstants.cs file ***/
            var dsConfig = new ClientDecisionService.MultiAction.DecisionServiceConfiguration<TestContext, DummyADFType>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new MultiWorldTesting.MultiAction.EpsilonGreedyExplorer<TestContext>(new TestMultiActionPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            bool isExceptionExpected = false;
            try
            {
                var ds = new ClientDecisionService.MultiAction.DecisionService<TestContext, DummyADFType>(dsConfig);
            }
            catch (InvalidDataException)
            {
                isExceptionExpected = true;
            }
            Assert.AreEqual(true, isExceptionExpected);
        }

    }
}
