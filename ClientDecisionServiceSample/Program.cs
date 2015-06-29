using ClientDecisionService;
using Microsoft.Research.DecisionService.Uploader;
using MultiWorldTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ClientDecisionServiceSample
{
    class Program
    {
        /***** Copy & Paste your authorization token here *****/
        static readonly string MwtServiceToken = "";

        static void Main(string[] args)
        {
            if (String.IsNullOrWhiteSpace(MwtServiceToken))
            {
                Console.WriteLine("Please specify a valid authorization token.");
                return;
            }
            SampleNewsRecommendation();
        }

        /// <summary>
        /// Sample code simulating a news recommendation scenario. In this simple example, 
        /// the rendering server has to pick 1 out of 10 articles to show to users (e.g. as featured article).
        /// In order to do so, it uses the <see cref="DecisionService{TContext}"/> API to optimize the decision
        /// to make given certain user context (or features).
        /// </summary>
        static void SampleNewsRecommendation()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());

            uint numArticles = 10; // number of different article choices to show
            float epsilon = 0.2f; // randomize the articles to show for 20% of traffic
            int numUsers = 100; // number of users for the news site

            var defaultPolicy = new NewsDisplayPolicy();

            // Create configuration for the decision service.
            var serviceConfig = new DecisionServiceConfiguration<UserContext>
            (
                authorizationToken: MwtServiceToken,

                // Specify the exploration algorithm to use, here we will use Epsilon-Greedy.
                // For more details about this and other algorithms, refer to the MWT onboarding whitepaper.
                explorer: new EpsilonGreedyExplorer<UserContext>(defaultPolicy, epsilon, numArticles)
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
                uint articleId = service.ChooseAction(uniqueKey: userId, context: userContext);

                // Display the article chosen by exploration process.
                DisplayNewsArticle(articleId);

                // Report {0,1} reward as a simple float.
                float reward = 1 - (user % 2);
                service.ReportReward(reward, uniqueKey: userId);
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
        static void SampleStandaloneUploader()
        {
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
        /// Displays the id of the chosen article.
        /// </summary>
        /// <param name="articleId">The article id.</param>
        static void DisplayNewsArticle(uint articleId)
        {
            Console.WriteLine("Article {0} was chosen.", articleId);
        }
    }

    /// <summary>
    /// Represents the user context as a sparse vector of float features.
    /// </summary>
    class UserContext : Dictionary<string, float> { }

    /// <summary>
    /// The default policy for choosing article to display given some user context.
    /// </summary>
    class NewsDisplayPolicy : IPolicy<UserContext>
    {
        /// <summary>
        /// Choose the action (or news article) to take given the specified context.
        /// </summary>
        /// <param name="context">The user context.</param>
        /// <returns>The action (or news article) to take.</returns>
        public uint ChooseAction(UserContext context)
        {
            // In this example, we are only picking among the first two articles.
            // This could simulate picking between the top 2 editorial picks.
            return (uint)(context.Count % 2 + 1);
        }
    }
}
