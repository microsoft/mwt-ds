using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiWorldTesting;
using Newtonsoft.Json;

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

    class MyPolicy : IPolicy<MyContext>
    {
        public uint ChooseAction(MyContext context)
        {
            // Always returns the same action regardless of context
            return 5;
        }
    }

    class Sample
    {
        static void Main(string[] args)
        {
            var serviceConfig = new DecisionServiceConfiguration<MyContext>();
            
            // Configure batching logic if desired
            //serviceConfig.BatchConfig = new BatchingConfiguration()
            //{
            //    Duration = TimeSpan.FromSeconds(30),
            //    EventCount = 1000,
            //    BufferSize = 2 * 1024 * 1024
            //};

            // Set a custom json serializer for the context
            //serviceConfig.ContextJsonSerializer = context => "My Context Json";

            var service = new DecisionService<MyContext>(serviceConfig);

            // TODO: We could also have a DecisionServicePolicy object to handle the model update.
            // TODO: We could have a DecisionService object that contains both the custom Recorder and Policy objects.

            var mwtt = new MwtExplorer<MyContext>("mwt", service.Recorder);

            // TODO: should we package these last parameters into a Configuration object
            var explorer = new EpsilonGreedyExplorer<MyContext>(new MyPolicy(), epsilon: 0.2f, numActions: 10);

            // This will call MyAzureRecorder.Record with the <x, a, p, k> tuple
            uint action = mwtt.ChooseAction(explorer, unique_key: "eventid", context: new MyContext());

            Console.WriteLine("Chosen action: {0}", action);

            // TODO: ReportOutcome can also be supported via MwtExplorer
            service.ReportOutcome(JsonConvert.SerializeObject(new MyOutcome()), null, uniqueKey: "eventid");
        }
    }
}
