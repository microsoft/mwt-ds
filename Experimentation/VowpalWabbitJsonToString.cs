using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using VW;
using VW.Serializer;

namespace Experimentation
{
    public static class VowpalWabbitJsonToString
    {
        public static void Convert(StreamReader reader, StreamWriter writer)
        {
            var line = reader.ReadLine();
            if (line == null)
                return;

            var jExample = JObject.Parse(line);
            var settings = jExample.Properties().Any(p => p.Name == "_multi") ? "--cb_explore_adf" : "--cb_explore";

            int lineNr = 1;
            using (var vw = new VowpalWabbit(new VowpalWabbitSettings(settings) {
                EnableStringExampleGeneration = true,
                EnableStringFloatCompact = true,
                EnableThreadSafeExamplePooling = true
            }))
            {
                var serializeBlock = new TransformBlock<Tuple<string, int>, string>(l =>
                {
                    using (var jsonSerializer = new VowpalWabbitJsonSerializer(vw))
                    using (var example = jsonSerializer.ParseAndCreate(l.Item1))
                    {
                        if (example == null)
                            throw new InvalidDataException($"Invalid example in line {l.Item2}: '{l.Item1}'");

                        var str = example.VowpalWabbitString;
                        if (example is VowpalWabbitMultiLineExampleCollection)
                            str += "\n";

                        return str;
                    }
                },
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 1024,
                    MaxDegreeOfParallelism = 8
                });

                var writeBlock = new ActionBlock<string>(
                    l => writer.WriteLine(l),
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1, BoundedCapacity = 128 });
                serializeBlock.LinkTo(writeBlock, new DataflowLinkOptions { PropagateCompletion = true });

                var input = serializeBlock.AsObserver();

                do
                {
                    input.OnNext(Tuple.Create(line, lineNr));
                    lineNr++;
                } while ((line = reader.ReadLine()) != null);

                input.OnCompleted();

                serializeBlock.Completion.Wait();
            }
        }
    }
}
