using System;
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
    public class CcbJsonParserTest
    {
        private ISchema CreateCcbBasicSchema()
        {
            return new USqlSchema(
                new USqlColumn<string>("EventId"),
                new USqlColumn<int>("SlotIdx"),
                new USqlColumn<string>("SessionId"),
                new USqlColumn<DateTime>("Timestamp")
                );
        }

        private ISchema CreateErrorHandlingSchema()
        {
            return new USqlSchema(
                new USqlColumn<string>("SessionId"),
                new USqlColumn<string>("ParseError")
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
        public void CcbTest()
        {
            var ccbLine = @"{ ""Timestamp"": ""2019-08-27T12:45:53.6300000Z"", ""Version"": ""1"", ""c"": { ""GUser"": { ""shared_feature"": ""feature"" }, ""_multi"": [ { ""TAction"": { ""feature1"": 3.0, ""feature2"": ""name1"" } }, { ""TAction"": { ""feature1"": 3.0, ""feature2"": ""name1"" } }, { ""TAction"": { ""feature1"": 3.0, ""feature2"": ""name1"" } } ], ""_slots"": [ { ""size"": ""small"", ""_inc"": [0, 2] }, { ""size"": ""large"" } ] }, ""_outcomes"": [ { ""_id"": ""62ddd79e-4d75-4c64-94f1-a5e13a75c2e4"", ""_label_cost"": 0, ""_a"": [2, 0], ""_p"": [0.9, 0.1], ""_o"": [] }, { ""_id"": ""042661c4-d433-4b05-83d6-d51a2d1c68be"", ""_label_cost"": 0, ""_a"": [1, 0], ""_p"": [0.1, 0.9], ""_o"": [-1.0, 0.0] } ], ""VWState"": { ""m"": ""da63c529-018b-44b1-ad0f-c2b13056832c/195fc8ed-224f-471a-90c4-d3e60b336f8f"" } }";
            var extractor = new CcbExtractor();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateCcbBasicSchema()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{ccbLine}")))
            {
                var input = new USqlStreamReader(stream);
                int counter = 0;
                foreach (var outputRow in extractor.Extract(input, output))
                {
                    if (counter == 0)
                    {
                        Assert.IsNotNull(output.Get<string>("SessionId"));
                        Assert.AreEqual(0, output.Get<int>("SlotIdx"));
                        Assert.AreEqual("62ddd79e-4d75-4c64-94f1-a5e13a75c2e4", output.Get<string>("EventId"));
                        //Assert.AreEqual(new DateTime(2019, 8, 23, 12,45, 53, 63), output.Get<DateTime>("Timestamp"));
                    }
                    else if (counter == 1)
                    {
                        Assert.IsNotNull(output.Get<string>("SessionId"));
                        Assert.AreEqual(1, output.Get<int>("SlotIdx"));
                        Assert.AreEqual("042661c4-d433-4b05-83d6-d51a2d1c68be", output.Get<string>("EventId"));
                    }
                    counter++;
                }
            }
        }

        [TestMethod]
        public void BadLinesHandlingTest()
        {
            var junk = @"blablabla";
            var ccbLine = @"{ ""Timestamp"": ""2019-08-27T12:45:53.6300000Z"", ""Version"": ""1"", ""c"": { ""GUser"": { ""shared_feature"": ""feature"" }, ""_multi"": [ { ""TAction"": { ""feature1"": 3.0, ""feature2"": ""name1"" } }, { ""TAction"": { ""feature1"": 3.0, ""feature2"": ""name1"" } }, { ""TAction"": { ""feature1"": 3.0, ""feature2"": ""name1"" } } ], ""_slots"": [ { ""size"": ""small"", ""_inc"": [0, 2] }, { ""size"": ""large"" } ] }, ""_outcomes"": [ { ""_id"": ""62ddd79e-4d75-4c64-94f1-a5e13a75c2e4"", ""_label_cost"": 0, ""_a"": [2, 0], ""_p"": [0.9, 0.1], ""_o"": [] }, { ""_id"": ""042661c4-d433-4b05-83d6-d51a2d1c68be"", ""_label_cost"": 0, ""_a"": [1, 0], ""_p"": [0.1, 0.9], ""_o"": [-1.0, 0.0] } ], ""VWState"": { ""m"": ""da63c529-018b-44b1-ad0f-c2b13056832c/195fc8ed-224f-471a-90c4-d3e60b336f8f"" } }";
            var extractor = new CcbExtractor();
            var output = new USqlUpdatableRow(CreateDefaultRow(CreateErrorHandlingSchema()));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes($"{junk}\n{ccbLine}")))
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
                        Assert.IsNotNull(output.Get<string>("SessionId"));
                        Assert.IsTrue(string.IsNullOrWhiteSpace(output.Get<string>("ParseError")));
                    }
                }
            }
        }
    }
}
