using Experimentation;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                var startTimeInclusive = new DateTime(2016, 8, 11, 0, 0, 0);
                var endTimeExclusive = new DateTime(2016, 8, 14, 0, 0, 0);
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
                using (var writer = new StreamWriter(outputFile + ".vw"))
                {
                    VowpalWabbitJsonToString.Convert(reader, writer);
                }

                // VW training
                OfflineTrainer.Train("--cb_explore_adf --epsilon 0.05 -q AB -q UD",
                    outputFile,
                    predictionFile: outputFile + ".2h.prediction",
                    reloadInterval: TimeSpan.FromHours(2));

                Metrics.Compute(outputFile, 
                    outputFile + ".prediction",
                    outputFile + ".2h.prediction");

                Console.WriteLine("\ndone " + stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}. {ex.StackTrace}");
            }

            Console.ReadKey();
        }
    }
}
