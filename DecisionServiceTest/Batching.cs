using DecisionSample;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;

namespace DecisionServiceTest
{
    [TestClass]
    public class Batching
    {
        [TestMethod]
        public void TestBatchingByCount()
        {
            var serviceConfig = new DecisionServiceConfiguration<TestContext>()
            {
                AppId = "mwt",
                Explorer = new EpsilonGreedyExplorer<TestContext>(new TestPolicy(), epsilon: 0.2f, numActions: 10),
                BatchConfig = new BatchingConfiguration()
                {
                    Duration = TimeSpan.FromSeconds(30),
                    EventCount = 10,
                    BufferSize = 10 * 1024 * 1024
                }
            };

            var service = new DecisionService<TestContext>(serviceConfig);

            string uniqueKey = "eventid";

            for (int i = 0; i < 4; i++)
            {
                uint action = service.ChooseAction(uniqueKey, new TestContext());
                // Report outcome as a JSON
                service.ReportOutcome("my json outcome", uniqueKey);
                // Report (simple) reward as a simple float
                service.ReportReward(0.5f, uniqueKey);
            }

            string json = System.IO.File.ReadAllText("decision_service_test_output");
            var batch = JsonConvert.DeserializeObject<EventBatch>(json);

        }
    }
}
