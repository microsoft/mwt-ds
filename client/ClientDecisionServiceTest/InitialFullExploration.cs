using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VW;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class InitialFullExploration : MockCommandTestBase
    {
        private class MyRecorder : IRecorder<string, int[]>
        {
            public GenericTopSlotExplorerState LastExplorerState { get; set; }

            public object LastMapperState { get; set; }

            public void Record(string context, int[] value, object explorerState, object mapperState, string uniqueKey)
            {
                this.LastExplorerState = (GenericTopSlotExplorerState)explorerState;
                this.LastMapperState = mapperState;
            }
        }

        [TestMethod]
        [TestCategory("Client Library")]
        [Priority(0)]
        public void InitialFullExplorationTest()
        {
            var recorder = new MyRecorder();

            using (var model = new MemoryStream())
            {
                using (var vw = new VowpalWabbit("--cb_explore_adf --epsilon 0.3"))
                {
                    vw.Learn(new[] { "1:-3:0.2 | b:2"});
                    vw.ID = "123";
                    vw.SaveModel(model);
                }

                var config = new DecisionServiceConfiguration("") { OfflineMode = true, OfflineApplicationID = "", DevelopmentMode = true };
                var metaData = new ApplicationClientMetadata
                {
                    TrainArguments = "--cb_explore_adf --epsilon 0.3",
                    InitialExplorationEpsilon = 1f
                };

                using (var ds = DecisionService.CreateJson(config, metaData:metaData).WithRecorder(recorder))
                {
                    var decision = ds.ChooseRanking("abc", "{\"a\":1,\"_multi\":[{\"b\":2}]}");

                    // since there's not a model loaded why should get 100% exploration
                    // Assert.AreEqual(1f, recorder.LastExplorerState.Probability);

                    model.Position = 0;
                    ds.UpdateModel(model);

                    decision = ds.ChooseRanking("abc", "{\"a\":1,\"_multi\":[{\"b\":2}, {\"b\":3}]}");
                    // Assert.AreNotEqual(1f, recorder.LastExplorerState.Probability);

                    var vwState = recorder.LastMapperState as VWState;
                    Assert.IsNotNull(vwState);
                    Assert.AreEqual("123", vwState.ModelId);
                }
            }
        }
    }
}
