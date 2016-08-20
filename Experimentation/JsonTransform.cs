using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Experimentation
{
    public static class JsonTransform
    {
        public static void TransformIgnoreProperties(string fileIn, string fileOut, params string[] propertiesToIgnore)
        {
            var ignorePropertiesSet = new HashSet<string>(propertiesToIgnore);
            Transform(fileIn, fileOut, (reader, writer) =>
            {
                if (reader.TokenType == JsonToken.PropertyName &&
                    ignorePropertiesSet.Contains((string)reader.Value))
                {
                    reader.Skip();
                    return true;
                }

                return false;
            });
        }

        public static void TransformFixMarginal(string fileOut, int numExpectedActions, char startingNamespace, TupleList<string, string> marginalProperties)
        {
            var serializer = JsonSerializer.CreateDefault();
            JsonTransform.Transform(fileOut, fileOut + ".fixed", (reader, writer) =>
            {
                var obj = (JObject)serializer.Deserialize(reader);
                var multi = (JArray)obj.SelectToken("$._multi");
                if (multi.Count == numExpectedActions)
                {
                    foreach (var item in multi)
                    {
                        for (int i = 0; i < marginalProperties.Count; i++)
                        {
                            var parentNodeName = marginalProperties[i].Item1;
                            var childNodeName = marginalProperties[i].Item2;
                            var parentNode = (JObject)item[parentNodeName];
                            var propertyValue = parentNode.SelectToken(childNodeName).Value<string>();
                            parentNode.Add($"{(char)(startingNamespace + i)}{childNodeName}", JToken.FromObject(new { c = "onstant", id = propertyValue }));
                        }
                    }
                    serializer.Serialize(writer, obj);
                }

                return true;
            });
        }

        public static void Transform(string fileIn, string fileOut, Func<JsonTextReader, JsonTextWriter, bool> transform)
        {
            using (var reader = new StreamReader(fileIn, Encoding.UTF8))
            using (var writer = new StreamWriter(fileOut, false, Encoding.UTF8))
            {
                var transformBlock = new TransformBlock<string, string>(
                    evt =>
                    {
                        var stringWriter = new StringWriter();
                        using (var jsonReader = new JsonTextReader(new StringReader(evt)))
                        using (var jsonWriter = new JsonTextWriter(stringWriter))
                        {
                            while (jsonReader.Read())
                            {
                                if (!transform(jsonReader, jsonWriter))
                                    jsonWriter.WriteToken(jsonReader.TokenType, jsonReader.Value);
                            }
                        }

                        return stringWriter.ToString();
                    },
                    new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = 1024,
                        MaxDegreeOfParallelism = 8 // TODO:parameterize
                    });

                var outputBock = new ActionBlock<string>(l => { if (!string.IsNullOrEmpty(l)) writer.WriteLine(l); },
                    new ExecutionDataflowBlockOptions { BoundedCapacity = 1024, MaxDegreeOfParallelism = 1 });
                transformBlock.LinkTo(outputBock, new DataflowLinkOptions { PropagateCompletion = true });

                var input = transformBlock.AsObserver();

                string line;
                while ((line = reader.ReadLine()) != null)
                    input.OnNext(line);

                input.OnCompleted();
                outputBock.Completion.Wait();
            }
        }
    }
}
