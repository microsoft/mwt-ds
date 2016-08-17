using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using VW;

namespace Experimentation
{
    internal class Label
    {
        [JsonProperty("as", NullValueHandling = NullValueHandling.Ignore)]
        internal int[] Actions;

        /// <summary>
        /// Ordered for Actions
        /// </summary>
        [JsonProperty("p", NullValueHandling = NullValueHandling.Ignore)]
        internal float[] Probabilities;

        [JsonProperty("a", NullValueHandling = NullValueHandling.Ignore)]
        internal int? Action;

        [JsonProperty("ac", NullValueHandling = NullValueHandling.Ignore)]
        internal int? ActionCount;

        [JsonProperty("pr", NullValueHandling = NullValueHandling.Ignore)]
        internal float? Probability;

        /// <summary>
        /// Ordred 0...n
        /// </summary>
        [JsonIgnore]
        internal float[] ProbabilitiesOrdered;

        [JsonProperty("pd", NullValueHandling = NullValueHandling.Ignore)]
        internal float? ProbabilityOfDrop = 0f;

        [JsonProperty("c")]
        internal float Cost;

        [JsonIgnore]
        internal int LineNr;
    }

    public static class Metrics
    {
        private static IEnumerable<Label> ParseCacheLabels(string labelFile)
        {
            //return FileTransformBlock.CreateBatch(
            //    labelFile,
            //    l =>
            //    {
            //        var label = JsonConvert.DeserializeObject<Label>(l.Content);

            //        if (label.Probabilities == null)
            //        {
            //            // reconstruct action/probabilities based on action/probDeprecated
            //            label.Probabilities = Enumerable.Repeat((float)label.Probability, (int)label.ActionCount).ToArray();

            //            label.Actions = new int[(int)(int)label.ActionCount];
            //            label.Actions[0] = (int)label.Action;
            //            for (int i = 1, j = 1; i < (int)label.ActionCount; i++, j++)
            //            {
            //                if (j == (int)label.ActionCount)
            //                    j++;

            //                label.Actions[i] = j;
            //            }
            //        }

            //        // order probs by action
            //        label.ProbabilitiesOrdered = new float[label.Actions.Length];
            //        for (int i = 0; i < label.Actions.Length; i++)
            //            label.ProbabilitiesOrdered[label.Actions[i] - 1] = label.Probabilities[i];

            //        label.LineNr = l.Number;
            //        return label;
            //    });

            using (var reader = new StreamReader(labelFile))
            {
                string line;
                int lineNr = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    var label = JsonConvert.DeserializeObject<Label>(line);

                    if (label.Probabilities == null)
                    {
                        // reconstruct action/probabilities based on action/probDeprecated
                        label.Probabilities = Enumerable.Repeat((float)label.Probability, (int)label.ActionCount).ToArray();

                        label.Actions = new int[(int)(int)label.ActionCount];
                        label.Actions[0] = (int)label.Action;
                        for (int i = 1, j = 1; i < (int)label.ActionCount; i++, j++)
                        {
                            if (j == (int)label.ActionCount)
                                j++;

                            label.Actions[i] = j;
                        }
                    }

                    // order probs by action
                    label.ProbabilitiesOrdered = new float[label.Actions.Length];
                    for (int i = 0; i < label.Actions.Length; i++)
                        label.ProbabilitiesOrdered[label.Actions[i] - 1] = label.Probabilities[i];

                    label.LineNr = lineNr++;
                    yield return label;
                }
            }
        }

        private static IEnumerable<Label> ExtractAndCacheLabels(string data)
        {
            var labelFile = data + ".labels";

            if (!File.Exists(labelFile))
            {
                Console.WriteLine($"Building label cache file {labelFile}");
                var jsonSerializer = new JsonSerializer();

                using (var writer = new StreamWriter(labelFile))
                using (var reader = new StreamReader(data))
                {
                    string line;
                    int lineNr = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        int? action = null;
                        float? cost = null;
                        float[] probabilities = null;
                        int[] actions = null;
                        float? probabilityOfDrop = null;
                        float? probDeprecated = null;
                        int? actionCount = null;

                        using (var jsonReader = new JsonTextReader(new StringReader(line)))
                        {
                            while (jsonReader.Read())
                            {
                                if (jsonReader.TokenType == JsonToken.PropertyName)
                                {
                                    if ("_label_action".Equals((string)jsonReader.Value, StringComparison.Ordinal))
                                        action = jsonReader.ReadAsInt32();
                                    else if ("_label_cost".Equals((string)jsonReader.Value, StringComparison.Ordinal))
                                        cost = (float)jsonReader.ReadAsDouble();
                                    else if ("_label_probability".Equals((string)jsonReader.Value, StringComparison.Ordinal))
                                        probDeprecated = (float)jsonReader.ReadAsDouble();
                                    else if ("_a".Equals((string)jsonReader.Value, StringComparison.Ordinal))
                                        actions = jsonSerializer.Deserialize<int[]>(jsonReader);
                                    else if ("_p".Equals((string)jsonReader.Value, StringComparison.Ordinal))
                                        probabilities = jsonSerializer.Deserialize<float[]>(jsonReader);
                                    else if ("_ProbabilityOfDrop".Equals((string)jsonReader.Value, StringComparison.Ordinal))
                                        probabilityOfDrop = (float)jsonReader.ReadAsDouble();
                                    else if ("_multi".Equals((string)jsonReader.Value, StringComparison.Ordinal))
                                    {
                                        if (!jsonReader.Read() && jsonReader.TokenType == JsonToken.StartArray)
                                        {
                                            throw new InvalidDataException($"Unexpected type for _multi: {jsonReader.TokenType}");
                                        }

                                        // count _multi elements
                                        actionCount = 0;
                                        while (jsonReader.Read() && jsonReader.TokenType == JsonToken.StartObject)
                                        {
                                            jsonReader.Skip();
                                            actionCount++;
                                        }

                                        if (jsonReader.TokenType != JsonToken.EndArray)
                                        {
                                            throw new InvalidDataException($"Unexpected type for _multi: {jsonReader.TokenType}");
                                        }
                                    }

                                    if (actions != null && cost != null && probabilities != null)
                                        break;
                                }
                            }
                        }

                        if ((actions == null && action == null) || cost == null || (probabilities == null && probDeprecated == null))
                            throw new InvalidDataException("Missing label in line " + lineNr);

                        var label = new Label
                        {
                            Cost = (float)cost,
                            ProbabilityOfDrop = probabilityOfDrop
                        };

                        if (actions == null || probabilities == null)
                        {
                            label.Action = action;
                            label.Probability = probDeprecated;
                            label.ActionCount = actionCount;
                        }
                        else
                        {
                            label.Actions = actions;
                            label.Probabilities = probabilities;
                        }

                        writer.WriteLine(JsonConvert.SerializeObject(label));

                        lineNr++;
                    }
                }
            }

            return ParseCacheLabels(labelFile);
        }

        internal class Data
        {
            [JsonProperty("nr")]
            public int LineNr { get; set; }

            [JsonProperty("as")]
            public int[] Actions { get; set; }

            [JsonProperty("a")]
            public int Action { get; set; }

            [JsonProperty("p")]
            public float[] Probabilities { get; set; }

            public float GetLoss(Label label)
            {
                if (Actions == null)
                    return VowpalWabbitContextualBanditUtil.GetUnbiasedCost((uint)label.Actions[0], (uint)Action, label.Cost, label.Probabilities[0]);
                else
                {
                    var c = Actions.Zip(Probabilities, (action, prob) => new { Action = action, Prob = prob })
                        .Sum(ap => ap.Prob * VowpalWabbitContextualBanditUtil.GetUnbiasedCost((uint)label.Actions[0], (uint)ap.Action + 1, label.Cost, label.Probabilities[0]));

                    var p = Actions.Zip(Probabilities, (action, prob) => new { Action = action, Prob = prob })
                                .Sum(ap => ap.Prob / (label.Probabilities[ap.Action] * (1 - label.ProbabilityOfDrop ?? 0)));

                    // SUM(cost) / SUM(1 / prob)
                    return c / (1f / p);
                }
            }
        }

        public static void Compute(string dataFile, params string[] predictions)
        {
            // deserialize in parallel
            // run all computation in parallel
            // average

            //var labelSource = ExtractAndCacheLabels(dataFile);
            //var messages = new List<IObservable<Tuple<string, float>>>();
            //for (int i = 0; i < predictions.Length; i++)
            //{
            //    var predFile = predictions[i];
            //    var pred = FileTransformBlock.CreateBatch(predFile, line => JsonConvert.DeserializeObject<Data>(line.Content));

            //    var joinBlock = new JoinBlock<List<Label>, List<Data>>(new GroupingDataflowBlockOptions { Greedy = false, BoundedCapacity = 128 });
            //    labelSource.LinkTo(joinBlock.Target1, new DataflowLinkOptions { PropagateCompletion = true });
            //    pred.LinkTo(joinBlock.Target2, new DataflowLinkOptions { PropagateCompletion = true });

            //    var lossBlock = new TransformBlock<Tuple<List<Label>, List<Data>>, float>(
            //        b =>
            //        {
            //            if (b.Item1.Count != b.Item2.Count)
            //                throw new InvalidDataException();

            //            var sum = 0f;
            //            for (int j = 0; j < b.Item1.Count; j++)
            //            {
            //                var label = b.Item1[j];
            //                var data = b.Item2[j];

            //                if (label.LineNr != data.LineNr)
            //                    throw new InvalidDataException($"Label line nr {label.LineNr} does not match prediction line number {data.LineNr}");

            //                if (data.Actions == null)
            //                    sum +=  VowpalWabbitContextualBanditUtil.GetUnbiasedCost((uint)label.Actions[0], (uint)data.Action, label.Cost, label.Probabilities[0]);
            //                else
            //                {
            //                    var c = data.Actions.Zip(data.Probabilities, (action, prob) => new { Action = action, Prob = prob })
            //                        .Sum(ap => ap.Prob * VowpalWabbitContextualBanditUtil.GetUnbiasedCost((uint)label.Actions[0], (uint)ap.Action + 1, label.Cost, label.Probabilities[0]));

            //                    var p = data.Actions.Zip(data.Probabilities, (action, prob) => new { Action = action, Prob = prob })
            //                                .Sum(ap => ap.Prob / (label.Probabilities[ap.Action] * (1 - label.ProbabilityOfDrop ?? 0)));

            //                    // SUM(cost) / SUM(1 / prob)
            //                    sum += c / (1f / p);
            //                }
            //            }

            //            return sum;
            //        },
            //        new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 4 });

            //    joinBlock.LinkTo(lossBlock, new DataflowLinkOptions { PropagateCompletion = true });

            //    messages.Add(lossBlock.AsObservable()
            //        .Sum()
            //        .Select(loss => Tuple.Create(predFile, loss)));
            //}


            //try
            //{
            //    // var labelCount = labelSource.AsObservable().Count().ToEnumerable().First();
            //    var labelCount = 1;
            //    var losses = messages.Aggregate((o1, o2) => o1.Merge(o2));

            //    foreach (var msg in losses.ToEnumerable())
            //        Console.WriteLine($"{msg.Item1:-30}: {msg.Item2 / labelCount}");
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine();
            //    throw;
            //}

            // TODO: labels are parsed multiple times
            var labelSource = ExtractAndCacheLabels(dataFile);
            Parallel.ForEach(predictions, pred =>
            {
                var loss = File.ReadLines(pred)
                    .Select(line => JsonConvert.DeserializeObject<Data>(line))
                    .Zip(labelSource, (data, label) => data.GetLoss(label))
                    .Average();

                Console.WriteLine($"{pred:-30}: {loss}");
            });
        }
    }
}
