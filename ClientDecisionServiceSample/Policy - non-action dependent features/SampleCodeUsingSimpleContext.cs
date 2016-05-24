using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceSample
{
    public static class Sample2
    {
        /***** Copy & Paste your authorization token here *****/
        static readonly string SettingsBlobUri = "";

        /// <summary>
        /// Sample code simulating a news recommendation scenario. In this simple example, 
        /// the rendering server has to pick 1 out of 10 news topics to show to users (e.g. as featured article).
        /// In order to do so, it uses the <see cref="DecisionService{TContext}"/> API to optimize the decision
        /// to make given certain simple context with a vector of features.
        /// </summary>
        public static void SampleCodeUsingSimpleContext()
        {
            if (String.IsNullOrWhiteSpace(SettingsBlobUri))
            {
                Console.WriteLine("Please specify a valid authorization token.");
                return;
            }

            Trace.Listeners.Add(new ConsoleTraceListener());

            // Create configuration for the decision service.
            var serviceConfig = new DecisionServiceConfiguration(settingsBlobUri: SettingsBlobUri)
            {
                JoinServiceBatchConfiguration = new BatchingConfiguration // Optionally configure batch upload
                {
                    MaxBufferSizeInBytes = 4 * 1024,
                    MaxDuration = TimeSpan.FromSeconds(5),
                    MaxEventCount = 1000,
                    MaxUploadQueueCapacity = 100,
                    UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
                }
            };
        }

        /// <summary>
        /// Displays the id of the chosen topic.
        /// </summary>
        /// <param name="topicId">The topic id.</param>
        /// <param name="userId">The user id.</param>
        private static void DisplayNewsTopic(int topicId, int userId)
        {
            Console.WriteLine("Topic {0} was chosen for user {1}.", topicId, userId);
        }
    }

    /// <summary>
    /// The default policy for choosing topic to display given some user context.
    /// </summary>
    class SimplePolicy : IPolicy<SimpleContext>
    {
        public PolicyDecision<int> MapContext(SimpleContext context)
        {
            // Return a constant action for simple demonstration.
            // In advanced scenarios, users can examine the context and return a more appropriate action.
            return 1;
        }
    }
}
