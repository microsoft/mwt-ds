using ClientDecisionService;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using MultiWorldTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ClientDecisionServiceSample
{
    class Program
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());

            try
            {
                TestServiceCommmunication();
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.Message + " " + e.StackTrace);
            }
        }

        private static void TestAzure()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=decisionservice;AccountKey=hAv42cl/DlFLwd+N23/wNQKub/nVSEYyO6zjlksgMFC9/HVhQMHpNVhdaZGTD1PT0W7lqfKbf9LVt2/z2K3Quw==");

            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            var containers = blobClient.ListContainers("testcomplete");

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };
            foreach (var c in containers)
            {
                Parallel.ForEach(c.ListBlobs(), parallelOptions, x => ((CloudBlockBlob)x).Delete());
            }

            CloudBlobContainer blobContainer = blobClient.GetContainerReference("complete");

            CloudBlockBlob blockBlob = blobContainer.GetBlockBlobReference("");

            DateTimeOffset lastBlobDate = new DateTimeOffset();
            IEnumerable<IListBlobItem> blobs = blobContainer.ListBlobs();
            foreach (IListBlobItem blobItem in blobs)
            {
                if (blobItem is CloudBlockBlob)
                {
                    var bbItem = (CloudBlockBlob)blobItem;
                    DateTimeOffset bbDate = bbItem.Properties.LastModified.GetValueOrDefault();
                    if (bbDate >= lastBlobDate)
                    {
                        blockBlob = bbItem;
                        lastBlobDate = bbDate;
                    }
                }
            }
        }

        private static void RetrainModel(string token, int numberOfActions)
        {
            // Int 
            string apiKey = "TmGRqNMWIZd9b9dDvXSF8AtK5t1NjCnxVB5Jm4QWH+AmAZf8YBFVMPrc3w+yqtW9hFf/hK6RhtdkfrRrCkmh5Q==";
            string baseAddress = "https://ussouthcentral.services.azureml-int.net/";
            string postAddress = "workspaces/6c6831ae655346a5be7cc6c9bb9dfefc/services/a2d00c3ceb2e414785ce707483327d54/jobs";
            string statusAddress = "workspaces/6c6831ae655346a5be7cc6c9bb9dfefc/services/a2d00c3ceb2e414785ce707483327d54/jobs/{0}?api-version=2.0";

            // Afxcurrent
            //string apiKey = "RCqaC3h/dKl9iAn75wefSSDeY67H2y3raHnRfvKOxWCKyYdOceBkVN/mfEn5f9yY0StliDYSifDO2MtTdkpUHw==";
            //string baseAddress = "https://afxcurrentrrs.cloudapp.net/";
            //string postAddress = "workspaces/2f3007c334664c069b1d1841166dc2d6/services/bdb537033b5d44869909c8e14e7fa9b4/jobs";
            //string statusAddress = "workspaces/2f3007c334664c069b1d1841166dc2d6/services/bdb537033b5d44869909c8e14e7fa9b4/jobs/{0}?api-version=2.0";
            //ServicePointManager.ServerCertificateValidationCallback += (a, b, c, d) => true;

            string jobId = string.Empty;
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(baseAddress);
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                if (String.IsNullOrWhiteSpace(jobId))
                {
                    var besInput = new AzureMLBESInputType
                    {
                        Input = null,
                        //Input = new AzureMLBESInputType.InputType
                        //{
                        //    ConnectionString = "DefaultEndpointsProtocol=https;AccountName=louiemartstorage;AccountKey=NQ7fHHuwvSnTERMNLAxGoz0Nf9LzV7Jl7651KhA1T3AB/i+/wTArWbV1TKowflIMEca6A53JkcwRsrind8P6+g==",
                        //    RelativeLocation = "dummy/dummy.csv",
                        //    SasBlobToken = null,
                        //    BaseLocation = null,
                        //},
                        Output = null,
                        GlobalParameters = new AzureMLBESInputType.GlobalParametersType
                        {
                            ReaderToken = token,
                            Token = token,
                            NumberOfActions = numberOfActions
                        }
                    };

                    Task<HttpResponseMessage> responseTask = client.PostAsync(
                        postAddress,
                        new StringContent(JsonConvert.SerializeObject(besInput), Encoding.UTF8, "application/json")
                    );
                    responseTask.Wait();

                    HttpResponseMessage response = responseTask.Result;
                    var t2 = response.Content.ReadAsStringAsync();
                    t2.Wait();

                    Console.WriteLine("REQUEST");
                    Console.WriteLine(t2.Result);
                    Console.WriteLine(response.Headers.ToString());

                    if (!response.IsSuccessStatusCode)
                    {

                        Trace.TraceError("Failed to retrain a new model through AzureML's batch execution service.");
                        Trace.WriteLine(t2.Result);
                        Trace.WriteLine(response.ReasonPhrase);
                        Trace.WriteLine(response.Headers.ToString());
                    }
                    else
                    {
                        Trace.TraceInformation("Model retraining succeeded");
                    }

                    jobId = t2.Result.Substring(1, t2.Result.Length - 2);
                }

                Console.WriteLine("POLLING RESULT");
                for (int i = 0; i < 20; i++)
                {
                    string getStatusAddress = string.Format(statusAddress, jobId);

                    var responseTask2 = client.GetAsync(getStatusAddress);
                    responseTask2.Wait();
                    var response2 = responseTask2.Result;

                    var t = response2.Content.ReadAsStringAsync();
                    t.Wait();

                    Console.WriteLine(t.Result);
                    Console.WriteLine(response2.Headers.ToString());

                    System.Threading.Thread.Sleep(2000);
                }
                
            }
        }

        private static void SampleCode()
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration<UserContext>(
                authorizationToken: "",
                explorer: new EpsilonGreedyExplorer<UserContext>(new UserPolicy(), epsilon: 0.2f, numActions: 10))
            //explorer = new TauFirstExplorer<MyContext>(new UserPolicy(), tau: 50, numActions: 10))
            //explorer = new BootstrapExplorer<MyContext>(new IPolicy<MyContext>[2] { new UserPolicy(), new UserPolicy() }, numActions: 10))
            //explorer = new SoftmaxExplorer<MyContext>(new UserScorer(), lambda: 0.5f, numActions: 10))
            //explorer = new GenericExplorer<MyContext>(new UserScorer(), numActions: 10))
            {
                // Configure batching logic if desired
                JoinServiceBatchConfiguration = new BatchingConfiguration()
                {
                    MaxDuration = TimeSpan.FromMilliseconds(5000),
                    MaxEventCount = 2,
                    MaxBufferSizeInBytes = 10 * 1024 * 1024,
                    MaxUploadQueueCapacity = 2,
                    UploadRetryPolicy = BatchUploadRetryPolicy.Retry
                },

                // Set a custom json serializer for the context
                //ContextJsonSerializer = context => "My Context Json",
            };

            var service = new DecisionService<UserContext>(serviceConfig);

            string uniqueKey = "eventid";
            uint action = service.ChooseAction(uniqueKey, new UserContext());

            // Report outcome as a JSON
            service.ReportOutcome("my json outcome", uniqueKey);
            // Report (simple) reward as a simple float
            service.ReportReward(0.5f, uniqueKey);

            // Synchronous flush
            service.Flush();
        }

        static void TestServiceCommmunication()
        {
            string rcv1File = null;
            switch (Environment.MachineName.ToLower())
            {
                case "lhoang2":
                    rcv1File = @"D:\Git\vw-louie\rcv1.train.multiclass.vw";
                    break;
                case "lhoang-pc7":
                    rcv1File = @"D:\Git\vw-louie\rcv1.train.multiclass.vw";
                    break;
                case "lhoang-surface":
                    rcv1File = @"C:\Users\lhoang\Documents\Git\vw-sid\dataset\rcv1.train.multiclass.vw";
                    break;
                case "marie":
                    rcv1File = @"D:\work\DecisionService\rcv1.train.multiclass.vw";
                    break;
                default:
                    rcv1File = @"rcv1.train.multiclass.vw";
                    break;
            }
            if (rcv1File == null || !File.Exists(rcv1File))
            {
                throw new Exception("Unable to find input file: " + rcv1File);
            }

            var serviceConfig = new DecisionServiceConfiguration<UserContext>(
                //authorizationToken: "c01ff675-5710-4814-a961-d03d2d6bce65",
                authorizationToken: "10198550-a074-4f9c-8b15-cc389bc2bbbe",
                explorer: new EpsilonGreedyExplorer<UserContext>(new UserPolicy(), epsilon: 0.2f, numActions: 2))
            {
                JoinServiceBatchConfiguration = new BatchingConfiguration()
                {
                    MaxDuration = TimeSpan.FromSeconds(1),
                    MaxEventCount = 1024 * 4,
                    MaxBufferSizeInBytes = 8 * 1024 * 1024,
                    MaxUploadQueueCapacity = 1024 * 32
                },
                // Features must be top-level, no nesting supported
                ContextJsonSerializer = uc => JsonConvert.SerializeObject(uc.FeatureVector)
            };

            var service = new DecisionService<UserContext>(serviceConfig);

            /*
            1 |f 13:3.9656971e-02 24:3.4781646e-02 69:4.6296168e-02 85:6.1853945e-02 140:3.2349996e-02 156:1.0290844e-01 175:6.8493
910e-02 188:2.8366476e-02 229:7.4871540e-02 230:9.1505975e-02 234:5.4200061e-02 236:4.4855952e-02 238:5.3422898e-02 387
:1.4059304e-01 394:7.5131744e-02 433:1.1118756e-01 434:1.2540409e-01 438:6.5452829e-02 465:2.2644201e-01 468:8.5926279e
-02 518:1.0214076e-01 534:9.4191484e-02 613:7.0990764e-02 646:8.7701865e-02 660:7.2289191e-02 709:9.0660661e-02 752:1.0
580081e-01 757:6.7965068e-02 812:2.2685185e-01 932:6.8250686e-02 1028:4.8203137e-02 1122:1.2381379e-01 1160:1.3038123e-
01 1189:7.1542501e-02 1530:9.2655659e-02 1664:6.5160148e-02 1865:8.5823394e-02 2524:1.6407280e-01 2525:1.1528353e-01 25
26:9.7131468e-02 2536:5.7415009e-01 2543:1.4978983e-01 2848:1.0446861e-01 3370:9.2423186e-02 3960:1.5554591e-01 7052:1.
2632671e-01 16893:1.9762035e-01 24036:3.2674628e-01 24303:2.2660980e-01
            */
            var transformBlock = new TransformBlock<Tuple<int, string>, Parsed>(t =>
            {
                string[] sections = t.Item2.Split(new string[] { "|f" }, StringSplitOptions.RemoveEmptyEntries);
                int trueAction = int.Parse(sections[0]);

                string[] features = sections[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                var featureVector = features.Select(f =>
                {
                    string[] ivPair = f.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                    return new
                    {
                        Index = int.Parse(ivPair[0]),
                        Val = float.Parse(ivPair[1])
                    };
                })
                    .ToDictionary(a => "f" + a.Index, a => a.Val);

                return new Parsed
                {
                    UniqueId = t.Item1.ToString(),
                    Context = new UserContext(featureVector),
                    TrueAction = trueAction
                };
            },
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = 1024 * 8,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                });

            var actionBlock = new ActionBlock<Parsed>(p =>
            {
                uint action = service.ChooseAction(p.UniqueId.ToString(), p.Context);
                service.ReportReward(-Math.Abs((int)action - p.TrueAction), p.UniqueId.ToString());
            });

            transformBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

            var obs = transformBlock.AsObserver();

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            using (var sr = new StreamReader(File.OpenRead(rcv1File)))
            {
                int lineNo = 1;
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();

                    obs.OnNext(Tuple.Create(lineNo++, line));

                    System.Threading.Thread.Sleep(100);

                    if (lineNo > 300)
                    {
                        break;
                    }
                }
            }
            Console.WriteLine("File reading done: " + stopwatch.Elapsed);

            transformBlock.Complete();
            actionBlock.Completion.Wait();

            Console.WriteLine("Processing done: " + stopwatch.Elapsed);

            service.Flush();

            Console.WriteLine("Service flushed done: " + stopwatch.Elapsed);
        }
    }

    class Parsed
    {
        internal string UniqueId { get; set; }

        internal UserContext Context { get; set; }

        internal int TrueAction { get; set; }
    }

    class UserContext
    {

        public UserContext() : this(null) { }

        public UserContext(IDictionary<string, float> features)
        {
            FeatureVector = features;
        }

        public IDictionary<string, float> FeatureVector { get; set; }
    }

    class MyOutcome { }

    class MyAzureRecorder : IRecorder<UserContext>
    {
        public void Record(UserContext context, UInt32 action, float probability, string uniqueKey)
        {
            // Stores the tuple in Azure.
        }
    }

    class UserPolicy : IPolicy<UserContext>
    {
        public uint ChooseAction(UserContext context)
        {
            return (uint)1; //((context.FeatureVector.Length % 2) + 1);
        }
    }

    class UserScorer : IScorer<UserContext>
    {
        public List<float> ScoreActions(UserContext context)
        {
            return new List<float>();
        }
    }

    public class AzureMLBESInputType
    {
        public InputType Input { get; set; }
        public string Output { get; set; }
        public GlobalParametersType GlobalParameters { get; set; }

        public class InputType
        {
            public string ConnectionString { get; set; }
            public string RelativeLocation { get; set; }
            public string BaseLocation { get; set; }
            public string SasBlobToken { get; set; }
        }

        public class GlobalParametersType
        {
            [JsonProperty(PropertyName = "Authorization Token")]
            public string ReaderToken { get; set; }

            [JsonProperty(PropertyName = "Decision Service Authorization Token")]
            public string Token { get; set; }

            [JsonProperty(PropertyName = "Number of actions")]
            public int NumberOfActions { get; set; }
        }
    }
}
