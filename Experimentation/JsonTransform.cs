using Newtonsoft.Json;
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
        public static void TransformIngoreProperties(string fileIn, string fileOut, params string[] propertiesToIgnore)
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

        public static void Transform(string fileIn, string fileOut, Func<JsonTextReader, JsonTextWriter, bool> transform)
        {
            using (var reader = new StreamReader(fileIn))
            using (var writer = new StreamWriter(fileOut))
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

                var outputBock = new ActionBlock<string>(l => writer.WriteLine(l),
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
