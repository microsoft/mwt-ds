using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiWorldTesting;
using Newtonsoft.Json;
using System.Threading.Tasks.Dataflow;

namespace DecisionSample
{
    class MyContext { }

    class MyOutcome { }

    class MyAzureRecorder : IRecorder<MyContext>
    {
        public void Record(MyContext context, UInt32 action, float probability, string uniqueKey)
        {
            // Stores the tuple in Azure.
        }
    }

    class UserPolicy : IPolicy<MyContext>
    {
        public uint ChooseAction(MyContext context)
        {
            // Always returns the same action regardless of context
            return 5;
        }
    }

    class UserScorer : IScorer<MyContext>
    {
        public List<float> ScoreActions(MyContext context)
        {
            return new List<float>();
        }
    }

    class Sample
    {
        static void Main(string[] args)
        {
            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration<MyContext>(
                appId: "mwt", 
                authorizationToken: "", 
                explorer: new EpsilonGreedyExplorer<MyContext>(new UserPolicy(), epsilon: 0.2f, numActions: 10))
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

            var service = new DecisionService<MyContext>(serviceConfig);

            string uniqueKey = "eventid";
            uint action = service.ChooseAction(uniqueKey, new MyContext());

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
    }
}
