using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VW.Serializer.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace ClientDecisionServiceTest
{
    [TestClass]
    public class LocalModeTest
    {
        [TestMethod]
        public void TestDSLocalInMemoryLogger()
        {
            // Logger for manually completed events
            var logger1 = new InMemoryLogger<FoodContext, int>(TimeSpan.MaxValue);
            // Logger that completes events automatically after 10ms (experimental unit duration)
            var logger2 = new InMemoryLogger<FoodContext, int>(new TimeSpan(0,0,0,0,10));
            var context = new FoodContext { Actions = new int[] { 1, 2, 3 }, UserLocation = "HealthyTown" };
            string guid1 = Guid.NewGuid().ToString();
            string guid2 = Guid.NewGuid().ToString();

            // Ensure manually completed events appear
            logger1.Record(context, 1, null, null, guid1);
            logger1.Record(context, 2, null, null, guid2);
            logger1.ReportRewardAndComplete(guid1, (float)2.0);
            logger1.ReportRewardAndComplete(guid2, (float)2.0);
            var dps1 = logger1.FlushCompleteEvents();
            Assert.IsTrue(dps1.Length == 2);
            string[] guids = { dps1[0].Key, dps1[1].Key };
            Assert.IsTrue(guids.Contains(guid1) && guids.Contains(guid2));
            
            // Ensure experimental unit duration works
            logger2.Record(context, 1, null, null, guid1);
            // The tick resolution in Windows is typically 15ms, so give some allowance
            Thread.Sleep(20);
            var dps2 = logger2.FlushCompleteEvents();
            Assert.IsTrue(dps2.Length == 1);
            // Since no reward was reported, the reward should be the default value
            Assert.IsTrue((dps2[0].Key == guid1) && (dps2[0].Reward == 0.0));

            // Use experimental unit and manually completed events simultaneously
            logger2.Record(context, 1, null, null, guid1);
            logger2.Record(context, 2, null, null, guid2);
            logger2.ReportRewardAndComplete(guid1, (float)2.0);
            dps2 = logger2.FlushCompleteEvents();
            Assert.IsTrue((dps2.Length == 1) && (dps2[0].Key == guid1));
            Thread.Sleep(50);
            dps2 = logger2.FlushCompleteEvents();
            Assert.IsTrue((dps2.Length == 1) && (dps2[0].Key == guid2));
            
            // Ensure multithreaded inserts yield correct results
            const int NumThreads = 16;
            const int NumEventsPerThread = 100;
            List<Thread> threads = new List<Thread>(NumThreads);
            for (int i = 0; i < NumThreads; i++)
            {
                threads.Add(new Thread(() =>
                    {
                        for (int j = 0; j < NumEventsPerThread; j++)
                        {
                            string guid = Guid.NewGuid().ToString();
                            // Test manual logger
                            logger1.Record(context, 1, null, null, guid);
                            logger1.ReportRewardAndComplete(guid, (float)3.0);
                            // Test experimental unit logger
                            logger2.Record(context, 1, null, null, guid);
                            logger2.ReportReward(guid, (float)4.0);
                        }
                    }));
            }
            foreach (Thread t in threads)
            {
                t.Start();
            }
            foreach (Thread t in threads)
            {
                t.Join();
            }
            dps1 = logger1.FlushCompleteEvents();
            Assert.IsTrue(dps1.Length == NumThreads * NumEventsPerThread);
            Thread.Sleep(50);
            dps2 = logger2.FlushCompleteEvents();
            Assert.IsTrue(dps2.Length == NumThreads * NumEventsPerThread);
            // Ensure the reward information was recorded before the event expired
            foreach (var dp in dps2)
            {
                Assert.IsTrue(dp.Reward == 4.0);
            }
        }

        [TestMethod]
        public void TestDSLocalModelUpdate()
        {
            string vwArgs = "--cb_explore_adf --epsilon 0.2 --cb_type dr -q ::";
            DecisionServiceLocal<FoodContext> dsLocal = new DecisionServiceLocal<FoodContext>(vwArgs, 1, TimeSpan.MaxValue);
            var context = new FoodContext { Actions = new int[] { 1, 2, 3 }, UserLocation = "HealthyTown" };
            string guid1 = Guid.NewGuid().ToString();
            string guid2 = Guid.NewGuid().ToString();
            byte[] prevModel = null;
            
            // Generate interactions and ensure the model updates at the right frequency
            // (updates every example initially)
            prevModel = dsLocal.Model;
            dsLocal.ChooseAction(guid1, context, 1);
            dsLocal.ReportRewardAndComplete((float)1.0, guid1);
            Assert.IsTrue(!dsLocal.Model.SequenceEqual(prevModel));

            // Set the model to update every two examples
            prevModel = dsLocal.Model;
            dsLocal.ModelUpdateInterval = 2;
            dsLocal.ChooseAction(guid1, context, 1);
            dsLocal.ReportRewardAndComplete((float)1.0, guid1);
            Assert.IsFalse(!dsLocal.Model.SequenceEqual(prevModel));
            dsLocal.ChooseAction(guid2, context, 1);
            dsLocal.ReportRewardAndComplete((float)2.0, guid1);
            Assert.IsTrue(!dsLocal.Model.SequenceEqual(prevModel));
        }
    }
}

