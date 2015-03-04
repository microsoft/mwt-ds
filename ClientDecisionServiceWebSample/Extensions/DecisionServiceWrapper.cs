using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MultiWorldTesting;
using ClientDecisionService;

namespace ClientDecisionServiceWebSample.Extensions
{
    public static class DecisionServiceWrapper<TContext>
    {
        public static EpsilonGreedyExplorer<TContext> Explorer { get; set; }
        public static DecisionServiceConfiguration<TContext> Configuration { get; set; }
        public static DecisionService<TContext> Service { get; set; }

        public static void Create(string modelOutputDir)
        {
            if (Explorer == null)
            {
                Explorer = new EpsilonGreedyExplorer<TContext>(new MartPolicy<TContext>(), .2f, 10);
            }

            if (Configuration == null)
            {
                //Configuration = new DecisionServiceConfiguration<TContext>("rcvtest", "c01ff675-5710-4814-a961-d03d2d6bce65", Explorer)
                Configuration = new DecisionServiceConfiguration<TContext>("louiemart", "c7b77291-f267-43da-8cc3-7df7ec2aeb06", Explorer)
                {
                    PolicyModelOutputDir = modelOutputDir,
                    BatchConfig = new BatchingConfiguration 
                    {
                        MaxDuration = TimeSpan.FromSeconds(5),
                        MaxBufferSizeInBytes = 10,
                        MaxEventCount = 1,
                        MaxUploadQueueCapacity = 1,
                        UploadRetryPolicy = BatchUploadRetryPolicy.Retry
                    }
                };
            }

            if (Service == null)
            {
                Service = new DecisionService<TContext>(Configuration);
            }
        }
    }

    class MartPolicy<TContext> : IPolicy<TContext>
    {
        public uint ChooseAction(TContext context)
        {
            return 5;
        }
    }
}