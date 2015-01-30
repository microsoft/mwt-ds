using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiWorldTesting;
using Newtonsoft.Json;
using System.Threading.Tasks.Dataflow;

namespace DecisionSample
{
    class Sample
    {
        static void Main(string[] args)
        {
            TestServiceCommmunication();
        }

        private static void SampleCode()
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration<UserContext>(
                appId: "mwt",
                authorizationToken: "",
                explorer: new EpsilonGreedyExplorer<UserContext>(new UserPolicy(), epsilon: 0.2f, numActions: 10))
                //explorer = new TauFirstExplorer<MyContext>(new UserPolicy(), tau: 50, numActions: 10))
                //explorer = new BootstrapExplorer<MyContext>(new IPolicy<MyContext>[2] { new UserPolicy(), new UserPolicy() }, numActions: 10))
                //explorer = new SoftmaxExplorer<MyContext>(new UserScorer(), lambda: 0.5f, numActions: 10))
                //explorer = new GenericExplorer<MyContext>(new UserScorer(), numActions: 10))
            {
                // Allowing model update. Users can suppress model update by setting this to False.
                IsPolicyUpdatable = true,

                // Configure batching logic if desired
                BatchConfig = new BatchingConfiguration()
                {
                    MaxDuration = TimeSpan.FromMilliseconds(5000),
                    MaxEventCount = 2,
                    MaxBufferSizeInBytes = 10 * 1024 * 1024,
                    MaxUploadQueueCapacity = 2
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

            service.FlushAsync().Wait();

            // TODO: We could also have a DecisionServicePolicy object to handle the model update.
            // TODO: We could have a DecisionService object that contains both the custom Recorder and Policy objects.

            // TODO: should we package these last parameters into a Configuration object
            //var explorer = new EpsilonGreedyExplorer<MyContext>(new UserPolicy(), epsilon: 0.2f, numActions: 10);

            // This will call MyAzureRecorder.Record with the <x, a, p, k> tuple
        }

        static void TestServiceCommmunication()
        {
            string rcv1File = null;
            switch (Environment.MachineName.ToLower())
            {
                case "lhoang-pc7":
                    rcv1File = @"D:\Git\vw-original\rcv1.train.multiclass.vw";
                    break;
                case "marie":
                    rcv1File = @"D:\work\DecisionService\rcv1.train.multiclass.vw";
                    break;
            }
            if (rcv1File == null)
            {
                throw new Exception();
            }

            var serviceConfig = new DecisionServiceConfiguration<UserContext>(
                appId: "rcvtest",
                authorizationToken: "c01ff675-5710-4814-a961-d03d2d6bce65",
                explorer: new EpsilonGreedyExplorer<UserContext>(new UserPolicy(), epsilon: 0.2f, numActions: 2))
            {
                BatchConfig = new BatchingConfiguration()
                {
                    MaxDuration = TimeSpan.FromMilliseconds(1000),
                    MaxEventCount = 100,
                    MaxBufferSizeInBytes = 2 * 1024 * 1024,
                    MaxUploadQueueCapacity = 10
                }
            };

            var service = new DecisionService<UserContext>(serviceConfig);

            using (var sr = new StreamReader(File.OpenRead(rcv1File)))
            {
                int lineNo = 1;
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();

                    string[] sections = line.Split(new string[] { "|f" }, StringSplitOptions.RemoveEmptyEntries);
                    int trueAction = Convert.ToInt32(sections[0]);

                    string[] features = sections[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    var featureVector = new List<float>(100000);
                    foreach (string f in features)
                    {
                        string[] ivPair = f.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                        int index = Convert.ToInt32(ivPair[0]);
                        float val = Convert.ToSingle(ivPair[1]);

                        while (index >= featureVector.Count)
                        {
                            featureVector.Add(0);
                        }
                        featureVector[index] = val;
                    }

                    uint action = service.ChooseAction(lineNo.ToString(), new UserContext(featureVector.ToArray()));
                    service.ReportReward(-Math.Abs((int)action - trueAction), lineNo.ToString());

                    lineNo++;
                }
            }
        }
    }

    class UserContext
    {

        public UserContext() : this(null) { }

        public UserContext(float[] features)
        {
            FeatureVector = features;
        }

        public float[] FeatureVector { get; set; }
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
            return (uint)((context.FeatureVector.Length % 2) + 1);
        }
    }

    class UserScorer : IScorer<UserContext>
    {
        public List<float> ScoreActions(UserContext context)
        {
            return new List<float>();
        }
    }
}
