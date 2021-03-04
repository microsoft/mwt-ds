﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Analytics.Interfaces;
using Microsoft.Analytics.UnitTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DecisionServiceExtractor.Test
{
    [TestClass]
    public class JsonParserTest
    {
        private ISchema CreateTestSchema()
        {
            return new USqlSchema(
                new USqlColumn<string>("EventId"),
                new USqlColumn<DateTime>("Timestamp"),
                new USqlColumn<float>("Cost"),
                new USqlColumn<float>("Prob"),
                new USqlColumn<int>("Action"),
                new USqlColumn<int>("NumActions"),
                new USqlColumn<int>("HasObservations"),
                new USqlColumn<string>("Data")
                );
        }

        private ISchema CreateTestSchemaWithPdrop()
        {
            return new USqlSchema(
                new USqlColumn<string>("EventId"),
                new USqlColumn<float>("pdrop")
                );
        }

        private ISchema CreateDanglingRewardSchema()
        {
            return new USqlSchema(
                new USqlColumn<string>("EventId"),
                new USqlColumn<DateTime>("EnqueuedTimeUtc"),
                new USqlColumn<float?>("RewardValue")
                );
        }

        private ISchema CreateMixedSchema()
        {
            return new USqlSchema(
                new USqlColumn<string>("EventId"),
                new USqlColumn<DateTime>("Timestamp"),
                new USqlColumn<DateTime>("EnqueuedTimeUtc"),
                new USqlColumn<bool>("IsDangling")
                );
        }

        private ISchema CreateErrorHandlingSchema()
        {
            return new USqlSchema(
                new USqlColumn<string>("EventId"),
                new USqlColumn<string>("ParseError")
                );
        }

        private ISchema CreateSkipLearnSchema()
        {
            return new USqlSchema(
                new USqlColumn<bool>("SkipLearn")
                );
        }


        private IRow CreateDefaultRow(ISchema schema)
        {
            var objects = new object[schema.Count];
            for (int i = 0; i < schema.Count; ++i)
            {
                objects[i] = schema[i].DefaultValue;
            }
            return new USqlRow(schema, objects);
        }


        [TestMethod]
        public void ActionCountTest()
        {
            var example = @"{""_label_cost"":-57,""_label_probability"":0.6,""_label_Action"":2,""_labelIndex"":1,""o"":[{""EventId"":""id"",""v"":1}],""Timestamp"":""2019-03-20T23:21:29.5020000Z"",""Version"":""1"",""EventId"":""id"",""a"":[0,1],""c"":{""GUser"":{""f1"":1},""_multi"":[{""a1"":{""af1"":1}},{""a80"":{""af1"":1}}]},""p"":[0.4,0.6],""VWState"":{""m"":""state_id""},""pdrop"":0.5}";
            var extractor = new HeaderOnly();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateTestSchema()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(example)))
            {
                var input = new USqlStreamReader(stream);
                foreach (var outputRow in extractor.Extract(input, output))
                {
                    Assert.AreEqual("id", output.Get<string>("EventId"));
                    Assert.AreEqual(-57.0, output.Get<float>("Cost"), 1e-6);
                    Assert.AreEqual(0.6, output.Get<float>("Prob"), 1e-6);
                    Assert.AreEqual(2, output.Get<int>("NumActions"));
                    Assert.AreEqual(2, output.Get<int>("Action"));
                    Assert.AreEqual(1, output.Get<int>("HasObservations"));
                }
            }
        }

        [TestMethod]
        public void ParseExistingPdropTest()
        {
            var exampleWithPdrop = @"{""_label_cost"":-57,""_label_probability"":0.6,""_label_Action"":2,""_labelIndex"":1,""Timestamp"":""2019-03-20T23:21:29.5020000Z"",""Version"":""1"",""EventId"":""id"",""a"":[0,1],""c"":{""GUser"":{""f1"":1},""_multi"":[{""a1"":{""af1"":1}},{""a80"":{""af1"":1}}]},""p"":[0.4,0.6],""VWState"":{""m"":""state_id""},""pdrop"":0.5}";
            var extractor = new HeaderOnly();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateTestSchemaWithPdrop()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(exampleWithPdrop)))
            {
                var input = new USqlStreamReader(stream);
                foreach (var outputRow in extractor.Extract(input, output))
                {
                    Assert.AreEqual("id", output.Get<string>("EventId"));
                    Assert.AreEqual(0.5, output.Get<float>("pdrop"));
                }
            }
        }

        [TestMethod]
        public void ParseNonExistingPdropTest()
        {
            var exampleWithPdrop = @"{""_label_cost"":-57,""_label_probability"":0.201250032,""_label_Action"":80,""_labelIndex"":79,""Timestamp"":""2019-03-20T23:21:29.000Z"",""Version"":""1"",""EventId"":""id"",""a"":[0,1],""c"":{""GUser"":{""f1"":1},""_multi"":[{""a1"":{""af1"":1}},{""a80"":{""af1"":1}}]},""p"":[0.4,0.6],""VWState"":{""m"":""state_id""}}";
            var extractor = new HeaderOnly();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateTestSchemaWithPdrop()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(exampleWithPdrop)))
            {
                var input = new USqlStreamReader(stream);
                foreach (var outputRow in extractor.Extract(input, output))
                {
                    Assert.AreEqual("id", output.Get<string>("EventId"));
                    Assert.AreEqual(0, output.Get<float>("pdrop"));
                }
            }
        }

        [TestMethod]
        public void ParseDanglingRewardTest()
        {
            var danglingReward = @"{""RewardValue"":846.7236,""ActionTaken"":false,""EnqueuedTimeUtc"":""2018-12-13T03:27:57.000Z"",""EventId"":""id"",""Observations"":[{""v"":846.7236,""ActionTaken"":false,""EventId"":""id"",""ActionId"":null}]}";
            var extractor = new HeaderOnly();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateDanglingRewardSchema()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(danglingReward)))
            {
                var input = new USqlStreamReader(stream);
                foreach (var outputRow in extractor.Extract(input, output))
                {
                    Assert.AreEqual("id", output.Get<string>("EventId"));
                    Assert.AreEqual(new DateTime(2018, 12, 13, 3, 27, 57), output.Get<DateTime>("EnqueuedTimeUtc"));
                    Assert.AreEqual(846.7236f, output.Get<float?>("RewardValue"));
                }
            }
        }

        [TestMethod]
        public void ParseActivationTest()
        {
            var danglingReward = @"{""RewardValue"":null,""ActionTaken"":true,""EnqueuedTimeUtc"":""2018-12-13T03:27:57.000Z"",""EventId"":""id"",""Observations"":[{""v"":""Unknown"",""ActionTaken"":true,""EventId"":""id"",""ActionId"":null}]}";
            var extractor = new HeaderOnly();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateDanglingRewardSchema()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(danglingReward)))
            {
                var input = new USqlStreamReader(stream);
                foreach (var outputRow in extractor.Extract(input, output))
                {
                    Assert.AreEqual("id", output.Get<string>("EventId"));
                    Assert.AreEqual(new DateTime(2018, 12, 13, 3, 27, 57), output.Get<DateTime>("EnqueuedTimeUtc"));
                    Assert.AreEqual(null, output.Get<float?>("RewardValue"));
                }
            }
        }

        [TestMethod]
        public void MixedParsingTest()
        {
            var interaction = @"{""_label_cost"":-57,""_label_probability"":0.201250032,""_label_Action"":80,""_labelIndex"":79,""Timestamp"":""2019-03-20T23:21:29.000Z"",""Version"":""1"",""EventId"":""id1"",""a"":[0,1],""c"":{""GUser"":{""f1"":1},""_multi"":[{""a1"":{""af1"":1}},{""a80"":{""af1"":1}}]},""p"":[0.4,0.6],""VWState"":{""m"":""state_id""}}";
            var danglingReward = @"{""RewardValue"":846.7236,""ActionTaken"":false,""EnqueuedTimeUtc"":""2018-12-13T03:27:57.000Z"",""EventId"":""id2"",""Observations"":[{""v"":846.7236,""ActionTaken"":false,""EventId"":""id"",""ActionId"":null}]}";
            var extractor = new HeaderOnly();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateMixedSchema()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{interaction}\n{danglingReward}")))
            {
                var input = new USqlStreamReader(stream);
                int counter = 0;
                foreach (var outputRow in extractor.Extract(input, output))
                {
                    if (counter++ == 0)
                    {
                        Assert.AreEqual("id1", output.Get<string>("EventId"));
                        Assert.AreEqual(new DateTime(2019, 03, 20, 23, 21, 29), output.Get<DateTime>("Timestamp"));
                        Assert.AreEqual(false, output.Get<bool>("IsDangling"));
                    }
                    else
                    {
                        Assert.AreEqual("id2", output.Get<string>("EventId"));
                        Assert.AreEqual(new DateTime(2018, 12, 13, 3, 27, 57), output.Get<DateTime>("EnqueuedTimeUtc"));
                        Assert.AreEqual(true, output.Get<bool>("IsDangling"));
                    }
                }
            }
        }


        [TestMethod]
        public void BadLinesHandlingTest()
        {
            var junk = @"blablabla";
            var danglingReward = @"{""RewardValue"":846.7236,""ActionTaken"":false,""EnqueuedTimeUtc"":""2018-12-13T03:27:57.000Z"",""EventId"":""id"",""Observations"":[{""v"":846.7236,""ActionTaken"":false,""EventId"":""id"",""ActionId"":null}]}";
            var extractor = new HeaderOnly();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateErrorHandlingSchema()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{junk}\n{danglingReward}")))
            {
                var input = new USqlStreamReader(stream);
                int counter = 0;
                foreach (var outputRow in extractor.Extract(input, output))
                {
                    if (counter++ == 0)
                    {
                        Assert.IsFalse(string.IsNullOrWhiteSpace(output.Get<string>("ParseError")));
                    }
                    else
                    {
                        Assert.AreEqual("id", output.Get<string>("EventId"));
                        Assert.IsTrue(string.IsNullOrWhiteSpace(output.Get<string>("ParseError")));
                    }
                }
            }
        }

        [TestMethod]
        public void SkipLearnTest()
        {
            var skipLearnFalse = @"{""_label_cost"":-1,""_label_probability"":0.866666973,""_label_Action"":3,""_labelIndex"":2,""_skipLearn"":false,""o"":[{""EventId"":""924da681-ea4f-4434-bc52-e9879393a052"",""v"":1.000000}],""Timestamp"":""2018-10-19T15:09:15.1100000Z"",""Version"":""1"",""EventId"":""924da681-ea4f-4434-bc52-e9879393a052"",""DeferredAction"":true,""a"":[3,2,1],""c"":{""User"":{""id"":""mk"",""major"":""psychology"",""hobby"":""kids"",""favorite_character"":""7of9""},""_multi"":[{""a"":{""topic"":""HerbGarden""}},{""a"":{""topic"":""MachineLearning""}},{""a"":{""topic"":""Football""}}]},""p"":[0.866667,0.066667,0.066667],""VWState"":{""m"":""model_id""}}";
            var skipLearnTrue = @"{""_label_cost"":-1,""_label_probability"":0.866666973,""_label_Action"":3,""_labelIndex"":2,""_skipLearn"":true,""o"":[{""EventId"":""924da681-ea4f-4434-bc52-e9879393a052"",""v"":1.000000}],""Timestamp"":""2018-10-19T15:09:15.1100000Z"",""Version"":""1"",""EventId"":""924da681-ea4f-4434-bc52-e9879393a052"",""DeferredAction"":true,""a"":[3,2,1],""c"":{""User"":{""id"":""mk"",""major"":""psychology"",""hobby"":""kids"",""favorite_character"":""7of9""},""_multi"":[{""a"":{""topic"":""HerbGarden""}},{""a"":{""topic"":""MachineLearning""}},{""a"":{""topic"":""Football""}}]},""p"":[0.866667,0.066667,0.066667],""VWState"":{""m"":""model_id""}}";
            var noSkipLearn = @"{""_label_cost"":-1,""_label_probability"":0.866666973,""_label_Action"":3,""_labelIndex"":2,""o"":[{""EventId"":""924da681-ea4f-4434-bc52-e9879393a052"",""v"":1.000000}],""Timestamp"":""2018-10-19T15:09:15.1100000Z"",""Version"":""1"",""EventId"":""924da681-ea4f-4434-bc52-e9879393a052"",""DeferredAction"":true,""a"":[3,2,1],""c"":{""User"":{""id"":""mk"",""major"":""psychology"",""hobby"":""kids"",""favorite_character"":""7of9""},""_multi"":[{""a"":{""topic"":""HerbGarden""}},{""a"":{""topic"":""MachineLearning""}},{""a"":{""topic"":""Football""}}]},""p"":[0.866667,0.066667,0.066667],""VWState"":{""m"":""model_id""}}";
            var extractor = new HeaderOnly();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateSkipLearnSchema()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{skipLearnFalse}\n{skipLearnTrue}\n{noSkipLearn}")))
            {
                var input = new USqlStreamReader(stream);
                int counter = 0;
                foreach (var outputRow in extractor.Extract(input, output))
                {
                    if (counter == 0)
                    {
                        Assert.IsFalse(output.Get<bool>("SkipLearn"));
                    }
                    else if (counter == 1)
                    {
                        Assert.IsTrue(output.Get<bool>("SkipLearn"));
                    }
                    else
                    {
                        Assert.IsFalse(output.Get<bool>("SkipLearn"));
                    }
                    counter++;
                }
            }
        }
    }
}
