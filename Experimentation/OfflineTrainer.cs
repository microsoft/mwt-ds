using Newtonsoft.Json;
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

        /// <summary>
        /// Train VW on offline data.
        /// </summary>
        /// <param name="arguments">Base arguments.</param>
        /// <param name="inputFile">Path to input file.</param>
        /// <param name="predictionFile">Name of the output prediction file.</param>
        /// <param name="reloadInterval">The TimeSpan interval to reload model.</param>
        /// <param name="learningRate">
        /// Learning rate must be specified here otherwise on Reload it will be reset.
        /// </param>
        /// <param name="cacheFilePrefix">
        /// The prefix of the cache file name to use. For example: prefix = "test" => "test.vw.cache"
        /// If none or null, the input file name is used, e.g. "input.dataset" => "input.vw.cache"
        /// !!! IMPORTANT !!!: Always use a new cache name if a different dataset or reload interval is used.
        /// </param>
        /// <remarks>
        /// Both learning rates and cache file are added to initial training arguments as well as Reload arguments.
        /// </remarks>
        public static void Train(string arguments, string inputFile, string predictionFile = null, TimeSpan? reloadInterval = null, float? learningRate=null, string cacheFilePrefix = null)
        {
            var learningArgs = learningRate == null ? string.Empty : $" -l {learningRate}";

            int cacheIndex = 0;
            var cacheArgs = (Func<int, string>)(i => $" --cache_file {cacheFilePrefix ?? Path.GetFileNameWithoutExtension(inputFile)}-{i}.vw.cache");

            using (var reader = new StreamReader(inputFile))
            using (var prediction = new StreamWriter(predictionFile ?? inputFile + ".prediction"))
            using (var vw = new VowpalWabbit(new VowpalWabbitSettings(arguments + learningArgs + cacheArgs(cacheIndex++))
            {
                Verbose = true
            }))
            {
                string line;
                int lineNr = 0;
                int invalidExamples = 0;
                DateTime? lastTimestamp = null;

                while ((line = reader.ReadLine()) != null)
                {
                    try
                    {
                        bool reload = false;
                        using (var jsonSerializer = new VowpalWabbitJsonSerializer(vw))
                        {
                            if (reloadInterval != null)
                            {
                                jsonSerializer.RegisterExtension((state, property) =>
                                {
                                    if (property.Equals("_timestamp", StringComparison.Ordinal))
                                    {
                                        var eventTimestamp = state.Reader.ReadAsDateTime();
                                        if (lastTimestamp == null)
                                            lastTimestamp = eventTimestamp;
                                        else if (lastTimestamp + reloadInterval < eventTimestamp)
                                        {
                                            reload = true;
                                            lastTimestamp = eventTimestamp;
                                        }

                                        return true;
                                    }

                                    return false;
                                });
                            }

                            // var pred = vw.Learn(line, VowpalWabbitPredictionType.ActionScore);
                            using (var example = jsonSerializer.ParseAndCreate(line))
                            {
                                var pred = example.Learn(VowpalWabbitPredictionType.ActionScore);

                                prediction.WriteLine(JsonConvert.SerializeObject(
                                    new
                                    {
                                        nr = lineNr,
                                        @as = pred.Select(x => x.Action),
                                        p = pred.Select(x => x.Score)
                                    }));
                            }

                            if (reload)
                                vw.Reload(learningArgs + cacheArgs(cacheIndex++));
                        }
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
