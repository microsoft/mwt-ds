using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ClientDecisionService;
using MultiWorldTesting;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Research.DecisionService.Common;
using Newtonsoft.Json;
using System.Web;
using JoinServerUploader;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class UploaderTest
    {
        [TestMethod]
        public void TestUploaderSingleEvent()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var uploader = new EventUploader(null, this.joinServerAddress);
            uploader.InitializeWithToken(this.authToken);
            uploader.Upload(new Interaction { Action = 1, Context = JsonConvert.SerializeObject(new TestContext()), Probability = 0.5, Key = uniqueKey });
            uploader.Flush();

            Assert.AreEqual(1, joinServer.RequestCount);
            Assert.AreEqual(1, joinServer.EventBatchList.Count);
            Assert.AreEqual(1, joinServer.EventBatchList[0].ExperimentalUnitFragments.Count);
            Assert.AreEqual(uniqueKey, joinServer.EventBatchList[0].ExperimentalUnitFragments[0].Id);
            Assert.IsTrue(joinServer.EventBatchList[0].ExperimentalUnitFragments[0].Value.ToLower().Contains("\"a\":1,"));
        }

        [TestMethod]
        public void TestUploaderMultipleEvents()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var uploader = new EventUploader(null, this.joinServerAddress);
            uploader.InitializeWithToken(this.authToken);
            uploader.Upload(new Interaction { Action = 1, Context = JsonConvert.SerializeObject(new TestContext()), Probability = 0.5, Key = uniqueKey });
            uploader.Upload(new Interaction { Action = 2, Context = JsonConvert.SerializeObject(new TestContext()), Probability = 0.7, Key = uniqueKey });
            uploader.Upload(new Interaction { Action = 0, Context = JsonConvert.SerializeObject(new TestContext()), Probability = 0.5, Key = uniqueKey });

            uploader.Upload(new Observation { Value = "1", Key = uniqueKey });
            uploader.Upload(new Observation { Value = "2", Key = uniqueKey });
            uploader.Upload(new Observation { Value = JsonConvert.SerializeObject(new { value = "test outcome" }), Key = uniqueKey });

            uploader.Flush();

            Assert.AreEqual(6, joinServer.EventBatchList.Sum(batch => batch.ExperimentalUnitFragments.Count));
        }

        [TestMethod]
        public void TestUploaderThreadSafe()
        {
            joinServer.Reset();

            string uniqueKey = "test interaction";

            var uploader = new EventUploader(null, this.joinServerAddress);
            uploader.InitializeWithToken(this.authToken);

            int numEvents = 1000;
            Parallel.For(0, numEvents, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, (i) =>
            {
                uploader.Upload(new Interaction { Action = i, Context = JsonConvert.SerializeObject(new TestContext()), Probability = i / 1000.0, Key = uniqueKey });
                uploader.Upload(new Observation { Value = JsonConvert.SerializeObject(new { value = "999" + i }), Key = uniqueKey });
            });
            uploader.Flush();

            Assert.AreEqual(numEvents * 2, joinServer.EventBatchList.Sum(batch => batch.ExperimentalUnitFragments.Count));
        }

        [TestInitialize]
        public void Setup()
        {
            joinServer = new MockJoinServer(joinServerAddress);

            joinServer.Run();
        }

        [TestCleanup]
        public void CleanUp()
        {
            joinServer.Stop();
        }

        private readonly string joinServerAddress = "http://localhost:9091/";
        private readonly string authToken = "test token";
        private MockJoinServer joinServer;
    }
}
