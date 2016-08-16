using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using VW;
using VW.Serializer;

namespace Experimentation
{
    public static class OfflineTrainer
    {
        //private class Event
        //{
        //    internal VowpalWabbitExampleCollection Example;

        //    internal string Line;

        //    internal int LineNr;

        //    internal ActionScore[] Prediction;
        //}

        public static void Train(string arguments, string inputFile)
        {
            using (var reader = new StreamReader(inputFile))
            using (var prediction = new StreamWriter(inputFile + ".prediction"))
            using (var vw = new VowpalWabbitJson(new VowpalWabbitSettings(arguments)
            {
                Verbose = true
            }))
            {
                string line;
                int lineNr = 0;
                int invalidExamples = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    try
                    {
                        var pred = vw.Learn(line, VowpalWabbitPredictionType.ActionScore);
                        prediction.WriteLine(lineNr + " " + string.Join(",", pred.Select(a_s => $"{a_s.Action}:{a_s.Score}")));
                    }
                    catch (Exception)
                    {
                        invalidExamples++;
                    }

                    lineNr++;
                }
            }

            // memory leak and not much gain below...
            //using (var vw = new VowpalWabbit(new VowpalWabbitSettings(arguments)
            //{
            //    Verbose = true,
            //    EnableThreadSafeExamplePooling = true,
            //    MaxExamples = 1024
            //}))
            //using (var reader = new StreamReader(inputFile))
            //using (var prediction = new StreamWriter(inputFile + ".prediction"))
            //{
            //    int invalidExamples = 0;

            //    var deserializeBlock = new TransformBlock<Event, Event>(
            //        evt =>
            //        {
            //            try
            //            {
            //                using (var vwJsonSerializer = new VowpalWabbitJsonSerializer(vw))
            //                {
            //                    evt.Example = vwJsonSerializer.ParseAndCreate(evt.Line);
            //                }
            //                // reclaim memory
            //                evt.Line = null;

            //                return evt;
            //            }
            //            catch (Exception)
            //            {
            //                Interlocked.Increment(ref invalidExamples);
            //                return null;
            //            }
            //        },
            //        new ExecutionDataflowBlockOptions
            //        {
            //            BoundedCapacity = 16,
            //            MaxDegreeOfParallelism = 8 // TODO: parameterize
            //        });

            //    var learnBlock = new TransformBlock<Event, Event>(
            //        evt =>
            //        {
            //            evt.Prediction = evt.Example.Learn(VowpalWabbitPredictionType.ActionScore);
            //            evt.Example.Dispose();
            //            return evt;
            //        },
            //        new ExecutionDataflowBlockOptions
            //        {
            //            BoundedCapacity = 64,
            //            MaxDegreeOfParallelism = 1
            //        });

            //    var predictionBlock = new ActionBlock<Event>(
            //        evt => prediction.WriteLine(evt.LineNr + " " + string.Join(",", evt.Prediction.Select(a_s => $"{a_s.Action}:{a_s.Score}"))),
            //        new ExecutionDataflowBlockOptions
            //        {
            //            BoundedCapacity = 16,
            //            MaxDegreeOfParallelism = 1
            //        });

            //    var input = deserializeBlock.AsObserver();

            //    deserializeBlock.LinkTo(learnBlock, new DataflowLinkOptions { PropagateCompletion = true }, evt => evt != null);
            //    deserializeBlock.LinkTo(DataflowBlock.NullTarget<object>());

            //    learnBlock.LinkTo(predictionBlock, new DataflowLinkOptions { PropagateCompletion = true });

            //    string line;
            //    int lineNr = 0;

            //    while ((line = reader.ReadLine()) != null)
            //        input.OnNext(new Event { Line = line, LineNr = lineNr++ });
            //    input.OnCompleted();

            //    predictionBlock.Completion.Wait();

                //Console.WriteLine($"Examples {lineNr}. Invalid: {invalidExamples}");
            //}
        }
    }
}
