using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClientDecisionService;
using MultiWorldTesting;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Text;
using Microsoft.Research.MultiWorldTesting.Contract;
using Newtonsoft.Json;
using System.Web;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class ModularTests
    {
        [TestMethod]
        public void TestOfflineModeArgument()
        {
            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                authorizationToken: "my token",
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: 2));

            dsConfig.OfflineMode = true;

            try
            {
                var ds = new DecisionService<TestContext>(dsConfig);
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("Recorder", ex.ParamName);
            }
        }

        [TestMethod]
        public void TestOfflineModeCustomLogger()
        {
            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                authorizationToken: "my token",
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            dsConfig.OfflineMode = true;
            dsConfig.Recorder = new TestLogger();

            int numChooseAction = 100;
            var ds = new DecisionService<TestContext>(dsConfig); 
            for (int i = 0; i < numChooseAction; i++)
            {
                ds.ChooseAction(i.ToString(), new TestContext());
            }

            Assert.AreEqual(numChooseAction, ((TestLogger)dsConfig.Recorder).NumRecord);

            int numReward = 200;
            for (int i = 0; i < numReward; i++)
            {
                ds.ReportReward(i, i.ToString());
            }

            Assert.AreEqual(numReward, ((TestLogger)dsConfig.Recorder).NumReward);

            int numOutcome = 300;
            for (int i = 0; i < numOutcome; i++)
            {
                ds.ReportOutcome(i.ToString(), i.ToString());
            }

            Assert.AreEqual(numOutcome, ((TestLogger)dsConfig.Recorder).NumOutcome);

            ds.Flush();

            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumRecord);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumReward);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumOutcome);
        }

        [TestMethod]
        public void TestOnlineModeCustomLogger()
        {
            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            dsConfig.Recorder = new TestLogger();

            int numChooseAction = 100;
            var ds = new DecisionService<TestContext>(dsConfig);
            for (int i = 0; i < numChooseAction; i++)
            {
                ds.ChooseAction(i.ToString(), new TestContext());
            }

            Assert.AreEqual(numChooseAction, ((TestLogger)dsConfig.Recorder).NumRecord);

            int numReward = 200;
            for (int i = 0; i < numReward; i++)
            {
                ds.ReportReward(i, i.ToString());
            }

            Assert.AreEqual(numReward, ((TestLogger)dsConfig.Recorder).NumReward);

            int numOutcome = 300;
            for (int i = 0; i < numOutcome; i++)
            {
                ds.ReportOutcome(i.ToString(), i.ToString());
            }

            Assert.AreEqual(numOutcome, ((TestLogger)dsConfig.Recorder).NumOutcome);

            ds.Flush();

            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumRecord);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumReward);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Recorder).NumOutcome);
        }

        [TestMethod]
        public void TestOnlineModeInvalidToken()
        {
            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                authorizationToken: MockCommandCenter.AuthorizationToken,
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            bool isExceptionExpected = false;
            try
            {
                var ds = new DecisionService<TestContext>(dsConfig);
            }
            catch (InvalidDataException)
            {
                isExceptionExpected = true;
            }
            Assert.AreEqual(true, isExceptionExpected);
        }
    }
}
