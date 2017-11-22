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
using VW.Serializer;
using VW;

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
        public async Task TestDSLocalModelUpdate()
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
            await dsLocal.ChooseActionAsync(guid1, context, 1);
            dsLocal.ReportRewardAndComplete((float)1.0, guid1);
            Assert.IsTrue(!dsLocal.Model.SequenceEqual(prevModel));

            // Set the model to update every two examples
            prevModel = dsLocal.Model;
            dsLocal.ModelUpdateInterval = 2;
            await dsLocal.ChooseActionAsync(guid1, context, 1);
            dsLocal.ReportRewardAndComplete((float)1.0, guid1);
            Assert.IsFalse(!dsLocal.Model.SequenceEqual(prevModel));
            await dsLocal.ChooseActionAsync(guid2, context, 1);
            dsLocal.ReportRewardAndComplete((float)2.0, guid1);
            Assert.IsTrue(!dsLocal.Model.SequenceEqual(prevModel));
        }

        [TestMethod]
        public void TestDSLocalModelLearning()
        {
            const int NumEvents = 100;
            const float Eps = 0.2f;
            string vwArgs = "--cb_explore_adf --epsilon " + Eps.ToString();
            // Test both generic class and json string typed versions of DS local
            DecisionServiceLocal<SimpleADFContext> dsLocal = new DecisionServiceLocal<SimpleADFContext>(vwArgs, 1, TimeSpan.MaxValue);
            DecisionServiceLocal<string> dsLocalJson = new DecisionServiceLocal<string>(vwArgs, 1, TimeSpan.MaxValue);
            var context = new SimpleADFContext { Id = "Shared", Actions = new int[] { 1, 2, 3 } };

            int action, actionJson;
            int targetActionCnt = 0, targetActionJsonCnt = 0;
            // Generate interactions and reward the model for the middle action only (learning the
            // lowest/highest can be done even with bad featurization, which we want to catch).
            for (int i = 0; i < NumEvents; i++)
            {
                string guid = Guid.NewGuid().ToString();
                // Test generic class type
                action = dsLocal.ChooseActionAsync(guid, context, 1).Result;
                dsLocal.ReportRewardAndComplete((action == 2) ? 1.0f : 0.0f, guid);
                targetActionCnt += (action == 2) ? 1 : 0;

                string contextJson = JsonConvert.SerializeObject(context);
                actionJson = dsLocalJson.ChooseActionAsync(guid, contextJson, 1).Result;
                //TODO: The examples should look identical to VW, so predictions should be identical
                //Assert.IsTrue(action == actionJson);
                dsLocalJson.ReportRewardAndComplete((actionJson == 2) ? 1.0f : 0.0f, guid);
                targetActionJsonCnt += (actionJson == 2) ? 1 : 0;
            }
            // Since the model is updated after each datapoint, we expect most exploit predictions 
            // (1 - Eps) to be the middle action, but allow fro some slack.
            Assert.IsTrue(targetActionCnt * 1.0 / NumEvents >= (1 - Eps)*0.9);
            Assert.IsTrue(targetActionJsonCnt * 1.0 / NumEvents >= (1 - Eps) * 0.9);
        }

        [TestMethod]
        public void TestDSLocalConcurrent()
        {
            const float Eps = 0.2f;
            string vwArgs = "--cb_explore_adf --epsilon " + Eps.ToString();
            DecisionServiceLocal<SimpleADFContext> dsLocal = new DecisionServiceLocal<SimpleADFContext>(vwArgs, 1, TimeSpan.MaxValue);
            var context = new SimpleADFContext { Id = "Shared", Actions = new int[] { 1, 2, 3 } };

            const int NumThreads = 16;
            const int NumEventsPerThread = 25;
            List<Thread> threads = new List<Thread>(NumThreads);
            int[] targetActionCnts = Enumerable.Repeat<int>(0, NumThreads).ToArray();
            int idCounter = 0;
            for (int i = 0; i < NumThreads; i++)
            {
                threads.Add(new Thread(() =>
                {
                    int id = Interlocked.Increment(ref idCounter) - 1;
                    Console.WriteLine("in thread {0}", id);
                    int action;
                    for (int j = 0; j < NumEventsPerThread; j++)
                    {
                        string guid = Guid.NewGuid().ToString();
                        action = dsLocal.ChooseActionAsync(guid, context, 1).Result;
                        dsLocal.ReportRewardAndComplete((action == 2) ? 1.0f : 0.0f, guid);
                        targetActionCnts[id] += (action == 2) ? 1 : 0;
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

            // Since the model is updated after each datapoint, we expect most exploit predictions 
            // (1 - Eps) to be the middle action, but allow fro some slack.
            Console.WriteLine("Sum of target is {0}, total is {1}", targetActionCnts.Sum(), NumThreads * NumEventsPerThread);
            Assert.IsTrue(targetActionCnts.Sum() * 1.0 / (NumThreads * NumEventsPerThread) >= (1 - Eps) * 0.9);
        }
    }

    /// <summary>
    /// Simple ADF context (similar to FoodContext but even simpler action dependent features)
    /// </summary>
    public class SimpleADFContext
    {
        public string Id { get; set; }

        [JsonIgnore]
        public int[] Actions { get; set; }

        [JsonProperty(PropertyName = "_multi")]
        public SimpleActionFeature[] ActionDependentFeatures
        {
            get
            {
                return this.Actions
                    .Select((i) => new SimpleActionFeature() { ActionId = i.ToString() })
                    .ToArray();
            }
        }

        public static IReadOnlyCollection<SimpleActionFeature> GetFeaturesFromContext(SimpleADFContext context)
        {
            return context.ActionDependentFeatures;
        }
    }

    public class SimpleActionFeature
    {
        // Note: If this action is numerical, it will appear as the same feature to VW across
        // all actions (only one weight will be learned). Further, absent other features the 0th 
        // action will be ignored, so VW will only see and explore over N-1 actions. 
        public string ActionId { get; set; }
    }
};

