using Experimentation;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using VW;
using VW.Serializer;

namespace ExperimentationConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                var storageAccount = new CloudStorageAccount(new StorageCredentials("storage name", "storage key"), false);

                var outputDirectory = @"c:\temp\";
                Directory.CreateDirectory(outputDirectory);
                var startTimeInclusive = new DateTime(2016, 8, 11, 19, 0, 0);
                var endTimeExclusive = new DateTime(2016, 8, 18, 0, 0, 0);
                var outputFile = Path.Combine(outputDirectory, $"{startTimeInclusive:yyyy-MM-dd_HH}-{endTimeExclusive:yyyy-MM-dd_HH}.json");

                // download and merge blob data
                using (var writer = new StreamWriter(outputFile))
                {
                    AzureBlobDownloader.Download(storageAccount, startTimeInclusive, endTimeExclusive, writer, outputDirectory).Wait();
                }

                // pre-process JSON
                JsonTransform.TransformIgnoreProperties(outputFile, outputFile + ".small",
                    "Somefeatures");

                outputFile += ".small";
                // filter broken events
                JsonTransform.Transform(outputFile, outputFile + ".fixed", (reader, writer) =>
                {
                    var serializer = JsonSerializer.CreateDefault();
                    var obj = (JObject)serializer.Deserialize(reader);
                    var multi = (JArray)obj.SelectToken("$._multi");
                    if (multi.Count == 10)
                        serializer.Serialize(writer, obj);

                    return true;
                });
                outputFile += ".fixed";

                using (var reader = new StreamReader(outputFile))
                using (var writer = new StreamWriter(new GZipStream(File.Create(outputFile + ".vw.gz"), CompressionLevel.Optimal)))
                {
                    VowpalWabbitJsonToString.Convert(reader, writer);
                }

                var bags = new[] { 1, 2, 4, 6, 8, 10 }.Select(a => "--bag " + a);
                var softmaxes = new[] { 0, 1, 2, 4, 8, 16, 32 }.Select(a => "--softmax --lambda " + a);
                var epsilons = new[] { .33333f, .2f, .1f, .05f }.Select(a => "--epsilon " + a);

                var arguments = Util.Expand(
                    epsilons.Union(bags).Union(softmaxes),
                    new[] { "--cb_type ips", "--cb_type mtr", "--cb_type dr" },
                    new[] { "-q AB -q UD" },
                    new[] { 0.005, 0.01, 0.02, 0.1 }.Select(l => string.Format(CultureInfo.InvariantCulture, "-l {0}", l))
                )
                .Select(a => $"--cb_explore_adf {a} --interact ud ")
                .ToList();

                var sep = "\t";
                var historyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mwt.experiments");
                using (var historyWriter = new StreamWriter(File.Open(historyFile, FileMode.OpenOrCreate)))
                {
                    for (int i = 0; i < arguments.Count; i++)
                    {
                        var startTime = DateTime.UtcNow;
                        var outputPredictionFile = $"{outputFile}.prediction";
                        var outputPrediction2hFile = $"{outputFile}.{i + 1}.2h.prediction";

                        // VW training
                        OfflineTrainer.Train(arguments[i],
                            outputFile,
                            predictionFile: outputPrediction2hFile,
                            reloadInterval: TimeSpan.FromHours(2),
                            cacheFilePrefix: null); // null to use input file's name for cache, see the method documentation for more details

                        var metricResult = Metrics.Compute(outputFile, outputPredictionFile, outputPrediction2hFile);

                        historyWriter.WriteLine($"{startTime}{sep}{arguments[i]}{sep}{string.Join(sep, metricResult.Select(m => m.Name + sep + m.Value))}");
                    }
                }

                Console.WriteLine("\ndone " + stopwatch.Elapsed);
                Console.WriteLine("Run information is added to: ", historyFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}. {ex.StackTrace}");
            }

            Console.ReadKey();
        }

        static void SweepCommandLine(string outputFile, string vwExe, string dataFile, string outModelDir, int numProcs = 30)
        {
            var bags = new[] { 1, 2, 4, 6, 8, 10 }.Select(a => "--bag " + a);
            var softmaxes = new[] { 0, 1, 2, 4, 8, 16, 32 }.Select(a => "--softmax --lambda " + a);
            var epsilons = new[] { .33333f, .2f, .1f, .05f }.Select(a => "--epsilon " + a);
            var covers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.Select(a => "--cover " + a);

            var arguments = Util.Expand(
                epsilons.Union(bags).Union(softmaxes),
                new[] { "--cb_type ips", "--cb_type mtr", "--cb_type dr" },
                new[] { "", "-q AB -q UD" },
                new[] { 0.005, 0.01, 0.02, 0.1, 0.2 }.Select(l => string.Format(CultureInfo.InvariantCulture, "-l {0}", l))
            )
            .Select((a, i) => $"--cb_explore_adf {a} -d {dataFile} -f {outModelDir}\\{i}.model -c")
            .ToList();

            File.WriteAllLines(outputFile, arguments);

            int numFinishedProcessing = 0;

            var inputBlock = new TransformBlock<int, int>(i => i);
            var processBlock = new ActionBlock<int>(i => 
            {
                var startTime = DateTime.UtcNow;
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = vwExe,
                    Arguments = arguments[i],
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                File.WriteAllText($"{outModelDir}\\{i}.output", $"{startTime}\r\n{arguments[i]}\r\n{output}\r\n{error}");
                p.WaitForExit();

                int numFinished = Interlocked.Increment(ref numFinishedProcessing);

                Console.WriteLine($"Finished: {numFinishedProcessing} / {arguments.Count}");
            },
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = numProcs, BoundedCapacity = numProcs });

            inputBlock.LinkTo(processBlock, new DataflowLinkOptions { PropagateCompletion = true });

            var input = inputBlock.AsObserver();

            for (int i = 0; i < arguments.Count; i++)
            {
                input.OnNext(i);
            }
            input.OnCompleted();

            processBlock.Completion.Wait();
        }

        static void AnalyzeSweep(string sweepDir, string sweepArgumentFile)
        {
            var regex = new Regex("average loss =(.*)");
            var args = File.ReadAllLines(sweepArgumentFile);

            var files = Directory.GetFiles(sweepDir, "*.output");

            using (var sw = new StreamWriter(File.Create(Path.Combine(sweepDir, "sweep.csv"))))
            {
                foreach (var f in files)
                {
                    var loss = Convert.ToSingle(regex.Match(File.ReadAllText(f)).Groups[1].Value.Trim());
                    var iArg = Convert.ToInt32(Path.GetFileNameWithoutExtension(f));
                    sw.WriteLine($"{args[iArg]},{loss}");
                }
            }
        }
    }
}
