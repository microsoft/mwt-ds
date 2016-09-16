using Microsoft.ApplicationInsights;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Priority_Queue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Experimentation
{
    public class AzureBlobDownloader
    {
        private class DownloadItem
        {
            internal CloudBlockBlob Blob;

            internal string Filename;

            internal DateTime DateTime;
        }

        private class DecisionServiceBlob
        {
            private TransformBlock<string, DecisionServiceEvent> buffer;
            private IObserver<string> bufferInput;
            private DownloadItem item;

            internal DecisionServiceBlob(DownloadItem item)
            {
                this.item = item;
                this.buffer = new TransformBlock<string, DecisionServiceEvent>(
                    (Func<string, DecisionServiceEvent>)this.Parse,
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        BoundedCapacity = 256
                    });

                bufferInput = this.buffer.AsObserver();

                // better to use dedicated thread to avoid communication to the thread pool for each line
                new Thread(this.Read).Start();
            }

            internal async Task<DecisionServiceEvent> Receive()
            {
                try
                {
                    // TODO: not sure how to avoid exception. checking before or after is not safe...
                    return await this.buffer.ReceiveAsync();
                }
                catch (InvalidOperationException)
                {
                    if (this.buffer.Completion.IsCompleted)
                        return null;

                    throw;
                }
            }

            internal DecisionServiceEvent Parse(string line)
            {
                using (var jsonReader = new JsonTextReader(new StringReader(line)))
                {
                    while (jsonReader.Read())
                    {
                        if (!(jsonReader.TokenType == JsonToken.PropertyName && "_timestamp".Equals((string)jsonReader.Value, StringComparison.Ordinal)))
                            continue;

                        return new DecisionServiceEvent
                        {
                            Line = line,
                            DateTime = (DateTime)jsonReader.ReadAsDateTime()
                        };
                    }
                }

                Console.WriteLine($"Invalid event found in {this.item.Blob.Uri}");
                // TODO: log invalid event?
                return null;
            }

            internal void Read()
            {
                StreamReader reader = null;
                try
                {
                    if (this.item.Filename != null)
                        reader = new StreamReader(this.item.Filename);
                    else
                        reader = new StreamReader(this.item.Blob.OpenRead());

                    string line;
                    while ((line = reader.ReadLine()) != null)
                        this.bufferInput.OnNext(line);

                    this.bufferInput.OnCompleted();
                }
                catch (Exception ex)
                {
                    this.bufferInput.OnError(ex);
                }
                finally
                {
                    if (reader != null)
                        reader.Dispose();
                }
            }
        }

        private class DecisionServiceEvent : FastPriorityQueueNode
        {
            internal string Line;

            internal DateTime DateTime;

            internal DecisionServiceBlob Blob;

            internal async Task<bool> Next()
            {
                var next = await Blob.Receive();
                if (next == null)
                    return false;

                Line = next.Line;
                DateTime = next.DateTime;

                return true;
            }
        }


        public static async Task Download(CloudStorageAccount storageAccount, DateTime startTimeInclusive, DateTime endTimeExclusive, StreamWriter writer, string cacheDirectory = null)
        {
            var telemetry = new TelemetryClient();

            var stopwatch = Stopwatch.StartNew();

            var blobClient = storageAccount.CreateCloudBlobClient();

            var blobsToDownload = new List<DownloadItem>();

            telemetry.TrackEvent($"Downloading {startTimeInclusive:yyyy-MM-dd} to {endTimeExclusive:yyyy-MM-dd}");

            long downloadedBytes = 0;
            var options = new BlobRequestOptions
            {
                MaximumExecutionTime = new TimeSpan(0, 60, 0),
                ServerTimeout = new TimeSpan(0, 60, 0),
                RetryPolicy = new ExponentialRetry()
            };
            var downloadBlock = new ActionBlock<DownloadItem>(async item =>
            {
                try
                {
                    if (cacheDirectory != null)
                        Console.WriteLine($"Downloading {item.Blob.Uri}");
                    var temp = item.Filename + ".temp";
                    await item.Blob.DownloadToFileAsync(temp, FileMode.Create, AccessCondition.GenerateEmptyCondition(), options, new OperationContext())
                        .ContinueWith(t => File.Move(temp, item.Filename));

                    Interlocked.Add(ref downloadedBytes, item.Blob.Properties.Length);
                }
                catch (Exception e)
                {
                    throw new Exception("Failed to download: " + item.Blob.Uri, e);
                }
            },
            new ExecutionDataflowBlockOptions { BoundedCapacity = 16, MaxDegreeOfParallelism = 8 });
            var downloadInput = downloadBlock.AsObserver();

            var eventIdToModelId = await DownloadTrackback(storageAccount, startTimeInclusive, endTimeExclusive);

            // enumerate full days
            for (var currentDateTime = new DateTime(startTimeInclusive.Year, startTimeInclusive.Month, startTimeInclusive.Day);
                currentDateTime < endTimeExclusive;
                currentDateTime += TimeSpan.FromDays(1))
            {
                foreach (var blob in blobClient.ListBlobs($"joined-examples/{currentDateTime:yyyy/MM/dd}", useFlatBlobListing: true).OfType<CloudBlockBlob>())
                {
                    // find relevant folder
                    var match = Regex.Match(blob.Uri.AbsolutePath, @"/(?<hour>\d{2})/(?<name>[^/]+\.json)$");
                    if (!match.Success)
                    {
                        telemetry.TrackTrace($"Skipping invalid blob '{blob.Uri}'.");
                        continue;
                    }

                    var blobDateTime = currentDateTime + TimeSpan.FromHours(int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture));
                    if (!(blobDateTime >= startTimeInclusive && blobDateTime < endTimeExclusive))
                        continue;


                    var item = new DownloadItem { Blob = blob, DateTime = blobDateTime };
                    blobsToDownload.Add(item);

                    if (cacheDirectory != null)
                    {
                        Console.WriteLine($"Checking {currentDateTime:yyyy/MM/dd}{match.Value}...");

                        // create directory
                        var outputDir = Path.Combine(cacheDirectory, $"{blobDateTime:yyyy/MM/dd/HH}");
                        if (!Directory.Exists(outputDir))
                            Directory.CreateDirectory(outputDir);

                        var outputFile = Path.Combine(outputDir, match.Groups["name"].Value);
                        item.Filename = outputFile;

                        if (!File.Exists(item.Filename))
                            downloadInput.OnNext(item);
                    }
                }
            }

            downloadInput.OnCompleted();
            await downloadBlock.Completion;

            if (cacheDirectory != null)
                Console.WriteLine($"Download time: {stopwatch.Elapsed}");

            foreach (var blobGroup in blobsToDownload.GroupBy(i => i.DateTime).OrderBy(i => i.Key))
            {
                if (cacheDirectory != null)
                    Console.WriteLine($"Merging {blobGroup.Key:yyyy/MM/dd HH}");

                // TODO: only use blob
                var dsBlobs = blobGroup.Select(b => new DecisionServiceBlob(b)).ToArray();

                // not really faster and more error prone
                //var priorityQueue = new FastPriorityQueue<DecisionServiceEvent>(dsBlobs.Length);
                var priorityQueue = new SimplePriorityQueue<DecisionServiceEvent>();

                foreach (var di in dsBlobs)
                {
                    var evt = await di.Receive();
                    if (evt != null)
                    {
                        evt.Blob = di;
                        priorityQueue.Enqueue(evt, evt.DateTime.Ticks);
                    }
                }

                while (true)
                {
                    var top = priorityQueue.First;

                    var jObject = JObject.Parse(top.Line);
                    var eventId = jObject["_eventid"].Value<string>();
                    var trainModelId = eventIdToModelId.ContainsKey(eventId) ? eventIdToModelId[eventId] : null;

                    jObject.Add("_model_id_train", trainModelId);

                    // add VW string format
                    writer.WriteLine(jObject.ToString(Formatting.None));

                    if (await top.Next())
                        priorityQueue.UpdatePriority(top, top.DateTime.Ticks);
                    else
                    {
                        priorityQueue.Dequeue();

                        if (priorityQueue.Count == 0)
                            break;
                    }
                }
            }

            if (cacheDirectory != null)
                Console.WriteLine($"Total time: {stopwatch.Elapsed}");
        }

        private static async Task<Dictionary<string, string>> DownloadTrackback(CloudStorageAccount storageAccount, DateTime startTimeInclusive, DateTime endTimeExclusive)
        {
            var telemetry = new TelemetryClient();
            var blobClient = storageAccount.CreateCloudBlobClient();
            telemetry.TrackEvent($"Downloading trackback {startTimeInclusive:yyyy-MM-dd} to {endTimeExclusive:yyyy-MM-dd}");

            var eventIdToModelId = new Dictionary<string, string>();
            for (var currentDateTime = new DateTime(startTimeInclusive.Year, startTimeInclusive.Month, startTimeInclusive.Day);
                currentDateTime < endTimeExclusive;
                currentDateTime += TimeSpan.FromDays(1))
            {
                var trackbackFormat = $"onlinetrainer/{currentDateTime:yyyyMMdd}";
                foreach (var blob in blobClient.ListBlobs(trackbackFormat, useFlatBlobListing: true).OfType<CloudBlockBlob>())
                {
                    var match = Regex.Match(blob.Uri.AbsolutePath, @".*\/\d*\/(\d*)\/.*.trackback");
                    if (!match.Success)
                    {
                        telemetry.TrackTrace($"Skipping invalid trackback blob '{blob.Uri}'.");
                        continue;
                    }

                    var blobDate = DateTime.ParseExact(match.Groups[1].Value, "HHmmss", CultureInfo.InvariantCulture);
                    var blobTime = blobDate - blobDate.Date;
                    var blobDateTime = currentDateTime + blobTime;
                    if (!(blobDateTime >= startTimeInclusive && blobDateTime < endTimeExclusive))
                        continue;

                    using (var stream = new MemoryStream())
                    using (var sr = new StreamReader(stream))
                    {
                        await blob.DownloadToStreamAsync(stream);
                        stream.Position = 0;
                        while (!sr.EndOfStream)
                        {
                            var eventId = sr.ReadLine().Trim();
                            if (eventIdToModelId.ContainsKey(eventId))
                            {
                                telemetry.TrackTrace($"Event Id {eventId} in trackback file {blob.Name} already exists.");
                            }
                            else
                            {
                                eventIdToModelId.Add(eventId, blob.Name.Replace(".trackback", string.Empty));
                            }
                        }
                    }
                }
            }
            return eventIdToModelId;
        }
    }
}
