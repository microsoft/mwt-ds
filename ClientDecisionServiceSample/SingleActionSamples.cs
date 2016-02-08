using Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;

namespace ClientDecisionServiceSample
{
    public static class SingleActionSamples
    {
        /***** Copy & Paste your authorization token here *****/
        static readonly string MwtServiceToken = "073576e3-d55e-470b-a633-def307ca408a";

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
            int numUsers = 100; // number of users for the news site
            int numFeatures = 20; // number of features for each user

            var defaultPolicy = new DefaultJsonPolicy();

            var serviceConfig = new DecisionServiceJsonConfiguration(
                authorizationToken: MwtServiceToken,
                // Specify the exploration algorithm to use, here we will use Epsilon-Greedy.
                // For more details about this and other algorithms, refer to the MWT onboarding whitepaper.
                explorer: new EpsilonGreedyExplorer<string>(defaultPolicy, epsilon, numTopics))
            {
                PollingForModelPeriod = TimeSpan.MinValue,
                PollingForSettingsPeriod = TimeSpan.MinValue,
                JoinServerType = Microsoft.Research.MultiWorldTesting.ClientLibrary.JoinServerType.AzureStreamAnalytics,
                EventHubConnectionString = "Endpoint=sb://mwtbus.servicebus.windows.net/;SharedAccessKeyName=shared-policy-scratch;SharedAccessKey=MqKvUJ/ZqBYC28izl0hgzdSmt9b3JvA2uUdncV4lRJA=",
                EventHubInputName = "eh-scratch",
                UseJsonContext = true // specify that context types are Json-formatted
            };

            // Optional: set the configuration for how often data is uploaded to the join server.
            serviceConfig.JoinServiceBatchConfiguration = new BatchingConfiguration
            {
                MaxBufferSizeInBytes = 4 * 1024,
                MaxDuration = TimeSpan.FromSeconds(5),
                MaxEventCount = 1000,
                MaxUploadQueueCapacity = 100,
                UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
            };

            // Create the main service object with above configurations.
            var service = new DecisionServiceJson(serviceConfig);

            var random = new Random();
            for (int user = 0; user < numUsers; user++)
            {
                // Generate a random GUID id for each user.
                var userId = Guid.NewGuid().ToString();

                // Generate random feature vector for each user.
                var features = Enumerable
                    .Range(user, numFeatures)
                    .Select(uid => new Feature { Id = (uint)uid, Value = (float)random.NextDouble() })
                    .ToArray();

                // Create the context object
                var userContext = JsonConvert.SerializeObject(new SimpleContext(features));

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

            // There shouldn't be any data in the buffer at this point 
            // but flush the service to ensure they are uploaded if otherwise.
            service.Flush();
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

            var defaultPolicy = new SimplePolicy();

            // Create configuration for the decision service.
            var serviceConfig = new DecisionServiceConfiguration<SimpleContext>
            (
                authorizationToken: MwtServiceToken,

                // Specify the exploration algorithm to use, here we will use Epsilon-Greedy.
                // For more details about this and other algorithms, refer to the MWT onboarding whitepaper.
                explorer: new EpsilonGreedyExplorer<SimpleContext>(defaultPolicy, epsilon, numTopics)
            );

            // Optional: set the configuration for how often data is uploaded to the join server.
            serviceConfig.JoinServiceBatchConfiguration = new BatchingConfiguration
            {
                MaxBufferSizeInBytes = 4 * 1024,
                MaxDuration = TimeSpan.FromSeconds(5),
                MaxEventCount = 1000,
                MaxUploadQueueCapacity = 100,
                UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
            };

            // Create the main service object with above configurations.
            var service = new DecisionService<SimpleContext>(serviceConfig);

            var random = new Random();
            for (int user = 0; user < numUsers; user++)
            {
                // Generate a random GUID id for each user.
                var userId = Guid.NewGuid().ToString();

                // Generate random feature vector for each user.
                var features = Enumerable
                    .Range(user, numFeatures)
                    .Select(uid => new Feature { Id = (uint)uid, Value = (float)random.NextDouble() })
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

            Console.WriteLine("DO NOT CLOSE THE CONSOLE WINDOW AT THIS POINT IF YOU ARE FOLLOWING THE GETTING STARTED GUIDE.");

            System.Threading.Thread.Sleep(TimeSpan.FromHours(24));

            // There shouldn't be any data in the buffer at this point 
            // but flush the service to ensure they are uploaded if otherwise.
            service.Flush();
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

            var defaultPolicy = new NewsDisplayPolicy();

            // Create configuration for the decision service.
            var serviceConfig = new DecisionServiceConfiguration<UserContext>
            (
                authorizationToken: MwtServiceToken,

                // Specify the exploration algorithm to use, here we will use Epsilon-Greedy.
                // For more details about this and other algorithms, refer to the MWT onboarding whitepaper.
                explorer: new EpsilonGreedyExplorer<UserContext>(defaultPolicy, epsilon, numTopics)
            );

            // Optional: set the configuration for how often data is uploaded to the join server.
            serviceConfig.JoinServiceBatchConfiguration = new BatchingConfiguration
            {
                MaxBufferSizeInBytes = 4 * 1024,
                MaxDuration = TimeSpan.FromSeconds(5),
                MaxEventCount = 1000,
                MaxUploadQueueCapacity = 100,
                UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
            };

            // Create the main service object with above configurations.
            var service = new DecisionService<UserContext>(serviceConfig);

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

            // There shouldn't be any data in the buffer at this point 
            // but flush the service to ensure they are uploaded if otherwise.
            service.Flush();
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

            // Initialize the uploader with a valid authorization token.
            uploader.InitializeWithToken(MwtServiceToken);

            // Specify the callback when a package of data was sent successfully.
            uploader.PackageSent += (sender, pse) => { Console.WriteLine("Uploaded {0} events.", pse.Records.Count()); };

            // Actual uploading of data.
            uploader.Upload(new SingleActionInteraction { Key = "sample-upload", Action = 1, Context = null, Probability = 0.5f });

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

    /// <summary>
    /// The default policy for choosing topic to display given some user context.
    /// </summary>
    class SimplePolicy : IPolicy<SimpleContext>
    {
        /// <summary>
        /// Choose the action to take given the specified context.
        /// </summary>
        /// <param name="context">The user context.</param>
        /// <returns>The action to take.</returns>
        public PolicyDecisionTuple ChooseAction(SimpleContext context, uint numActionsVariable = uint.MaxValue)
        {
            // Return a constant action for simple demonstration.
            // In advanced scenarios, users can examine the context and return a more appropriate action.
            return new PolicyDecisionTuple { Action = 1 };
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
        /// <summary>
        /// Choose the action (or news topic) to take given the specified context.
        /// </summary>
        /// <param name="context">The user context.</param>
        /// <returns>The action (or news topic) to take.</returns>
        public PolicyDecisionTuple ChooseAction(UserContext context, uint numActionsVariable = uint.MaxValue)
        {
            // In this example, we are only picking among the first two topics.
            // This could simulate picking between the top 2 editorial picks.
            return new PolicyDecisionTuple
            {
                Action = (uint)(Math.Round(context.Sum(f => f.Value) / context.Count + 1))
            };
        }
    }
}
