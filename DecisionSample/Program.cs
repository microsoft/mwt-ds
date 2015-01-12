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
            DecisionServiceRecorder<MyContext> recorder = new DecisionServiceRecorder<MyContext>(
                new RetryStorageConfiguration()
                {
                    EndpointType = StorageEndpoint.LocalDisk,
                    FilePath = @"C:\MyFile.type"
                }
                //, new BatchingConfiguration()
                //{
                //    Duration = TimeSpan.FromSeconds(30),
                //    EventCount = 1000,
                //    BufferSize = 2 * 1024 * 1024
                //}
            );
            MwtExplorer<MyContext> mwtt = new MwtExplorer<MyContext>("mwt", recorder);
            EpsilonGreedyExplorer<MyContext> explorer = new EpsilonGreedyExplorer<MyContext>(new MyPolicy(), epsilon: 0.2f, numActions: 10);

            // This will call MyAzureRecorder.Record with the <x, a, p, k> tuple
            uint action = mwtt.ChooseAction(explorer, unique_key: "eventid", context: new MyContext());

            Console.WriteLine("Chosen action: {0}", action);

            recorder.ReportOutcome(new MyOutcome(), uniqueKey: "eventid");
        }
    }
}
