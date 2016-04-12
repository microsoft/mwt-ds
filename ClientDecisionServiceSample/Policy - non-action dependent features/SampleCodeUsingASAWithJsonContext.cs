using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceSample
{
    public static class Sample1
    {
        /***** Copy & Paste your authorization token here *****/
        static readonly string MwtServiceToken = "";

        /***** Copy & Paste your EventHub configurations here *****/
        static readonly string EventHubConnectionString = "";
        static readonly string EventHubInputName = "";

        /// <summary>
        /// Sample code simulating a news recommendation scenario. In this simple example, 
        /// the rendering server has to pick 1 out of 10 news topics to show to users (e.g. as featured article).
        /// In order to do so, it uses the <see cref="DecisionService{TContext}"/> API to optimize the decision
        /// to make given certain simple context with a vector of features.
        /// </summary>
        public static void SampleCodeUsingASAWithJsonContext()
        {
            if (String.IsNullOrWhiteSpace(MwtServiceToken))
            {
                Console.WriteLine("Please specify a valid authorization token.");
                return;
            }

            Trace.Listeners.Add(new ConsoleTraceListener());

            int numTopics = 10; // number of different topic choices to show
            float epsilon = 0.2f; // randomize the topics to show for 20% of traffic
            int numUsers = 10; // number of users for the news site
            int numFeatures = 20; // number of features for each user

            var serviceConfig = new DecisionServiceConfiguration(authorizationToken: MwtServiceToken)
            {
                EventHubConnectionString = EventHubConnectionString,
                EventHubInputName = EventHubInputName,
                JoinServiceBatchConfiguration = new BatchingConfiguration // Optionally configure batch upload
                {
                    MaxBufferSizeInBytes = 4 * 1024,
                    MaxDuration = TimeSpan.FromSeconds(5),
                    MaxEventCount = 1000,
                    MaxUploadQueueCapacity = 100,
                    UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
                }
            };

            // Create the main service object with above configurations.
            // Specify the exploration algorithm to use, here we will use Epsilon-Greedy.
            // For more details about this and other algorithms, refer to the MWT onboarding whitepaper.
            using (var service = DecisionService.WithJsonPolicy(serviceConfig).WithEpsilonGreedy(epsilon, numTopics).ExploitUntilModel(new MyJsonPolicy()))
            {
                var random = new Random();
                for (int user = 0; user < numUsers; user++)
                {
                    // Generate a random GUID id for each user.
                    var userId = Guid.NewGuid().ToString();

                    // Generate random feature vector for each user.
                    var features = Enumerable
                        .Range(user, numFeatures)
                        .Select(uid => (float)random.NextDouble())
                        .ToArray();

                    // Create the context object
                    var userContext = JsonConvert.SerializeObject(new SimpleContext(features));

                    var timestamp = DateTime.UtcNow;

                    // Perform exploration given user features.
                    int topicId = service.ChooseAction(new UniqueEventID { Key = userId, TimeStamp = timestamp }, context: userContext);

                    // Display the news topic chosen by exploration process.
                    DisplayNewsTopic(topicId, user + 1);

                    // Report {0,1} reward as a simple float.
                    // In a real scenario, one could associated a reward of 1 if user
                    // clicks on the article and 0 otherwise.
                    float reward = 1 - (user % 2);
                    service.ReportReward(reward, new UniqueEventID { Key = userId, TimeStamp = timestamp });

                    System.Threading.Thread.Sleep(1);
                }

                System.Threading.Thread.Sleep(TimeSpan.FromMinutes(10));
            }
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

    public class MyJsonPolicy : IPolicy<string>
    {
        public PolicyDecision<int> MapContext(string context)
        {
            return 1;
        }
    }
}
