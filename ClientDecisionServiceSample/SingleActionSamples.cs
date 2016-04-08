using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;

namespace ClientDecisionServiceSample
{
    /// <summary>
    /// Sample code for using the Decision Service when the decision type
    /// involves a single action.
    /// </summary>
    public static class SingleActionSamples
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

            uint numTopics = 10; // number of different topic choices to show
            float epsilon = 0.2f; // randomize the topics to show for 20% of traffic
            int numUsers = 10; // number of users for the news site
            int numFeatures = 20; // number of features for each user

            var serviceConfig = new DecisionServiceConfiguration(authorizationToken: MwtServiceToken)
            {
                EventHubConnectionString = SingleActionSamples.EventHubConnectionString,
                EventHubInputName = SingleActionSamples.EventHubInputName,
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
            var policy = VWPolicy.StartWithJsonPolicy(serviceConfig, new DefaultJsonPolicy());
            using (var service = DecisionServiceClient.Create(policy.WithEpsilonGreedy(epsilon, numTopics)))
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
                    uint topicId = service.ChooseAction(new UniqueEventID { Key = userId, TimeStamp = timestamp }, context: userContext);

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
        /// Sample code simulating a news recommendation scenario. In this simple example, 
        /// the rendering server has to pick 1 out of 10 news topics to show to users (e.g. as featured article).
        /// In order to do so, it uses the <see cref="DecisionService{TContext}"/> API to optimize the decision
        /// to make given certain simple context with a vector of features.
        /// </summary>
        public static void SampleCodeUsingSimpleContext()
        {
            if (String.IsNullOrWhiteSpace(MwtServiceToken))
            {
                Console.WriteLine("Please specify a valid authorization token.");
                return;
            }

            Trace.Listeners.Add(new ConsoleTraceListener());

            uint numTopics = 10; // number of different topic choices to show
            float epsilon = 0.2f; // randomize the topics to show for 20% of traffic
            int numUsers = 100; // number of users for the news site
            int numFeatures = 20; // number of features for each user

            // Create configuration for the decision service.
            var serviceConfig = new DecisionServiceConfiguration(authorizationToken: MwtServiceToken)
            {
                EventHubConnectionString = SingleActionSamples.EventHubConnectionString,
                EventHubInputName = SingleActionSamples.EventHubInputName,
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
            var policy = VWPolicy.StartWithPolicy(serviceConfig, new SimplePolicy());
            using (var service = DecisionServiceClient.Create(policy.WithEpsilonGreedy(epsilon, numTopics)))
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
                    var userContext = new SimpleContext(features);

                    // Perform exploration given user features.
                    uint topicId = service.ChooseAction(new UniqueEventID { Key = userId }, context: userContext);

                    // Display the news topic chosen by exploration process.
                    DisplayNewsTopic(topicId, user + 1);

                    // Report {0,1} reward as a simple float.
                    // In a real scenario, one could associated a reward of 1 if user
                    // clicks on the article and 0 otherwise.
                    float reward = 1 - (user % 2);
                    service.ReportReward(reward, new UniqueEventID { Key = userId });
                }
            }
        }

        /// <summary>
        /// Sample code simulating a news recommendation scenario. In this simple example, 
        /// the rendering server has to pick 1 out of 10 news topics to show to users (e.g. as featured article).
        /// In order to do so, it uses the <see cref="DecisionService{TContext}"/> API to optimize the decision
        /// to make given certain user context (or features).
        /// </summary>
        public static void SampleNewsRecommendation()
        {
            if (String.IsNullOrWhiteSpace(MwtServiceToken))
            {
                Console.WriteLine("Please specify a valid authorization token.");
                return;
            }

            Trace.Listeners.Add(new ConsoleTraceListener());

            uint numTopics = 10; // number of different topic choices to show
            float epsilon = 0.2f; // randomize the topics to show for 20% of traffic
            int numUsers = 100; // number of users for the news site

            // Create configuration for the decision service.
            var serviceConfig = new DecisionServiceConfiguration(authorizationToken: MwtServiceToken)
            {
                // Optional: set the configuration for how often data is uploaded to the join server.
                JoinServiceBatchConfiguration = new BatchingConfiguration
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
            var policy = VWPolicy.StartWithPolicy(serviceConfig, new NewsDisplayPolicy());
            using (var service = DecisionServiceClient.Create(policy.WithEpsilonGreedy(epsilon, numTopics)))
            {
                var random = new Random();
                for (int user = 0; user < numUsers; user++)
                {
                    // Generate a random GUID id for each user.
                    var userId = Guid.NewGuid().ToString();

                    // Generate random feature vector for each user.
                    var userContext = new UserContext();
                    for (int f = 1; f <= 10; f++)
                    {
                        userContext.Add(f.ToString(), (float)random.NextDouble());
                    }

                    // Perform exploration given user features.
                    uint topicId = service.ChooseAction(new UniqueEventID { Key = userId }, context: userContext);

                    // Display the news topic chosen by exploration process.
                    DisplayNewsTopic(topicId, user + 1);

                    // Report {0,1} reward as a simple float.
                    // In a real scenario, one could associated a reward of 1 if user
                    // clicks on the article and 0 otherwise.
                    float reward = 1 - (user % 2);
                    service.ReportReward(reward, new UniqueEventID { Key = userId });
                }

                Console.WriteLine("DO NOT CLOSE THE CONSOLE WINDOW AT THIS POINT IF YOU ARE FOLLOWING THE GETTING STARTED GUIDE.");

                System.Threading.Thread.Sleep(TimeSpan.FromHours(24));
            }
        }

        /// <summary>
        /// Sample code for using the standalone <see cref="EventUploader"/> API to upload data to the join server. 
        /// </summary>
        public static void SampleStandaloneUploader()
        {
            if (String.IsNullOrWhiteSpace(MwtServiceToken))
            {
                Console.WriteLine("Please specify a valid authorization token.");
                return;
            }

            var uploader = new EventUploader();

            // TODO: remove this sample code since in-mem join server is no longer supported?
            // Initialize the uploader with a valid authorization token.
            uploader.InitializeWithToken(MwtServiceToken);

            // Specify the callback when a package of data was sent successfully.
            uploader.PackageSent += (sender, pse) => { Console.WriteLine("Uploaded {0} events.", pse.Records.Count()); };

            // Actual uploading of data.
            uploader.Upload(Interaction.CreateEpsilonGreedy(key: "sample-upload", context: "sample context", action: 1, probability: 0.5f));

            // Flush to ensure any remaining data is uploaded.
            uploader.Flush();
        }

        /// <summary>
        /// Displays the id of the chosen topic.
        /// </summary>
        /// <param name="topicId">The topic id.</param>
        /// <param name="userId">The user id.</param>
        private static void DisplayNewsTopic(uint topicId, int userId)
        {
            Console.WriteLine("Topic {0} was chosen for user {1}.", topicId, userId);
        }
    }

    class SimpleContext
    {
        public SimpleContext(float[] features)
        {
            this.Features = features;
        }

        public float[] Features { get; set; }
    }

    /// <summary>
    /// The default policy for choosing topic to display given some user context.
    /// </summary>
    class SimplePolicy : IPolicy<SimpleContext>
    {
        public Decision<uint> MapContext(SimpleContext context)
        {
            // Return a constant action for simple demonstration.
            // In advanced scenarios, users can examine the context and return a more appropriate action.
            return 1;
        }
    }

    /// <summary>
    /// Represents the user context as a sparse vector of float features.
    /// </summary>
    class UserContext : Dictionary<string, float> { }

    /// <summary>
    /// The default policy for choosing topic to display given some user context.
    /// </summary>
    class NewsDisplayPolicy : IPolicy<UserContext>
    {
        public Decision<uint> MapContext(UserContext context)
        {
            return Decision.Create((uint)(Math.Round(context.Sum(f => f.Value) / context.Count + 1)));
        }
    }

    public class DefaultJsonPolicy : IPolicy<string>
    {
        public Decision<uint> MapContext(string context)
        {
            return 1;
        }
    }
}