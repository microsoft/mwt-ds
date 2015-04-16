using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClientDecisionService;
using MultiWorldTesting;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class UnitTest1
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
                Assert.AreEqual("Logger", ex.ParamName);
            }
        }

        [TestMethod]
        public void TestOfflineModeLogger()
        {
            var dsConfig = new DecisionServiceConfiguration<TestContext>(
                authorizationToken: "my token",
                explorer: new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: Constants.NumberOfActions));

            dsConfig.OfflineMode = true;
            dsConfig.Logger = new TestLogger();

            int numChooseAction = 100;
            var ds = new DecisionService<TestContext>(dsConfig); 
            for (int i = 0; i < numChooseAction; i++)
            {
                ds.ChooseAction(i.ToString(), new TestContext());
            }

            Assert.AreEqual(numChooseAction, ((TestLogger)dsConfig.Logger).NumRecord);

            int numReward = 200;
            for (int i = 0; i < numReward; i++)
            {
                ds.ReportReward(i, i.ToString());
            }

            Assert.AreEqual(numReward, ((TestLogger)dsConfig.Logger).NumReward);

            int numOutcome = 300;
            for (int i = 0; i < numOutcome; i++)
            {
                ds.ReportOutcome(i.ToString(), i.ToString());
            }

            Assert.AreEqual(numOutcome, ((TestLogger)dsConfig.Logger).NumOutcome);

            ds.Flush();

            Assert.AreEqual(0, ((TestLogger)dsConfig.Logger).NumRecord);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Logger).NumReward);
            Assert.AreEqual(0, ((TestLogger)dsConfig.Logger).NumOutcome);
        }
    }
}
