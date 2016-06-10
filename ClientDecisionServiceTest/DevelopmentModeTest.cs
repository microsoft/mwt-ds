using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class DevelopmentModeTest : MockCommandTestBase
    {
        [TestMethod]
        public void TestDevModeSettingsAndExampleLog()
        {
            joinServer.Reset();

            var testTraceListener = new TestTraceListener();
            Trace.Listeners.Add(testTraceListener);

            string vwArgs = "--cb_explore_adf --epsilon 0.5";

            commandCenter.CreateBlobs(createSettingsBlob: true, createModelBlob: false, vwArgs: vwArgs);

            var dsConfig = new DecisionServiceConfiguration(MockCommandCenter.SettingsBlobUri)
            {
                JoinServerType = JoinServerType.CustomSolution,
                LoggingServiceAddress = MockJoinServer.MockJoinServerAddress,
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue,
                DevelopmentMode = true
            };

            int numInteractionEvents = 25;
            var eventIdList = new List<string>();

            using (var ds = DecisionService
                .Create<TestADFContextWithFeatures>(dsConfig)
                // .With<TestADFContextWithFeatures, TestADFFeatures>(context => context.ActionDependentFeatures)
                // TODO .WithTopSlotEpsilonGreedy(.5f)
                .ExploitUntilModelReady(new ConstantPolicy<TestADFContextWithFeatures>(ctx => ctx.ActionDependentFeatures.Count)))
            {
                byte[] modelContent = commandCenter.GetCBADFModelBlobContent(numExamples: 5, numFeatureVectors: 10, vwDefaultArgs: vwArgs);
                using (var modelStream = new MemoryStream(modelContent))
                {
                    ds.UpdateModel(modelStream);
                }
                for (int i = 1; i <= numInteractionEvents; i++)
                {
                    var interId = "inter" + i;
                    var obserId = "obser" + i;
                    Random rg = new Random(i);
                    int numActions = rg.Next(5, 20);
                    var context = TestADFContextWithFeatures.CreateRandom(numActions, rg);
                    int[] action = ds.ChooseRanking(interId, context);
                    ds.ReportReward(i / 100f, obserId);

                    eventIdList.Add(interId);
                    eventIdList.Add(obserId);
                }
            }
            // Number of batches must be exactly equal to number of events uploaded in development mode
            // and each batch must contain exactly one event
            Assert.AreEqual(numInteractionEvents * 2, joinServer.EventBatchList.Count);
            Assert.AreEqual(numInteractionEvents * 2, joinServer.EventBatchList.Sum(ebl => ebl.ExperimentalUnitFragments.Count));
            var eblList = joinServer.EventBatchList.Select(ebl => ebl.ExperimentalUnitFragments[0].Id).OrderBy(id => id);
            Assert.IsTrue(eblList.SequenceEqual(eventIdList.OrderBy(id => id)));

            // Trace messages must contain context information
            Assert.AreEqual(numInteractionEvents, testTraceListener.Messages.Count(m => m.Contains("Example Context")));
        }

    }

    public class TestTraceListener : TraceListener
    {
        public List<string> Messages { get; set; }

        public TestTraceListener()
        {
            Messages = new List<string>();
        }

        public override void Write(string message)
        {
            Messages.Add(message);
        }

        public override void WriteLine(string message)
        {
            Messages.Add(message);
        }
    }
}
