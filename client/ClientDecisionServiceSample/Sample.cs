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
    public static class Sample
    {
        /***** Copy & Paste your application settings URL here *****
         ***** This value can either be found after deployment *****
         ***** or in the Management Center of Decision Service *****/
        static readonly string SettingsBlobUri = "";

        /// <summary>
        /// Sample code simulating a news recommendation scenario. In this simple example, 
        /// the rendering server has to pick 1 out of 10 news topics to show to users (e.g. as featured article).
        /// </summary>
        /// <remarks>
        /// NOTE: For this sample to work, the proper settings must be set at deployment time:
        /// Number Of Actions = 10
        /// Vowpal Wabbit Switches = --cb_explore 10 --epsilon 0.2 --cb_type dr
        /// </remarks>
        public static void NewsRecommendation()
        {
            if (String.IsNullOrWhiteSpace(SettingsBlobUri))
            {
                Console.WriteLine("Please specify a valid settings URL.");
                return;
            }

            int numTopics = 10; // number of different topic choices to show

            // Create configuration for the decision service.
            var serviceConfig = new DecisionServiceConfiguration(settingsBlobUri: SettingsBlobUri);
            
            // Enable development mode to easily debug / diagnose data flow and system properties
            // This should be turned off in production deployment
            serviceConfig.DevelopmentMode = true;

            // Create the main service object with above configurations.
            // Specify the exploration algorithm to use, here we will use Epsilon-Greedy.
            // For more details about this and other algorithms, refer to the MWT onboarding whitepaper.
            using (var service = DecisionService.Create<UserContext>(serviceConfig))
            {
                // Create a default policy which is used before a machine-learned model.
                // This policy is used in epsilon-greedy exploration using the initial
                // exploration epsilon specified at deployment time or in the Management Center.
                var defaultPolicy = new NewsDisplayPolicy(numTopics);

                var random = new Random();
                int user = 0;

                Console.WriteLine("Press Ctrl + C at any time to cancel the process.");

                int maxDecisionHistory = 100;
                var correctDecisions = new Queue<int>();
                while (true)
                {
                    user++;

                    // Generate a random GUID id for each user.
                    var uniqueKey = Guid.NewGuid().ToString();

                    // Generate random feature vector for each user.
                    var userContext = new UserContext()
                    {
                        Gender = random.NextDouble() > 0.5 ? "male" : "female"
                    };
                    for (int f = 1; f <= 10; f++)
                    {
                        userContext.Features.Add(f.ToString(), (float)random.NextDouble());
                    }

                    // Perform exploration given user features.
                    int topicId = service.ChooseAction(uniqueKey, userContext, defaultPolicy);

                    // Display the news topic chosen by exploration process.
                    Console.WriteLine("Topic {0} was chosen for user {1}.", topicId, user + 1);

                    // Report {0,1} reward as a simple float.
                    // In a real scenario, one could associated a reward of 1 if user
                    // clicks on the article and 0 otherwise.
                    float reward = 0;
                    if (userContext.Gender == "male" && topicId == 3)
                    {
                        reward = 1;
                    }
                    else if (userContext.Gender == "female" && topicId == 8)
                    {
                        reward = 1;
                    }
                    service.ReportReward(reward, uniqueKey);

                    correctDecisions.Enqueue((int)reward);
                    if (correctDecisions.Count == maxDecisionHistory)
                    {
                        correctDecisions.Dequeue();
                    }
                    if (user % 50 == 0)
                    {
                        Console.WriteLine("Correct decisions out of last {0} interactions: {1}", maxDecisionHistory, correctDecisions.Sum());
                    }
                    System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(50));
                }
            }
        }
    }

    /// <summary>
    /// The default policy for choosing topic to display given some user context.
    /// </summary>
    class NewsDisplayPolicy : IPolicy<UserContext>
    {
        private int numTopics;

        public NewsDisplayPolicy(int numTopics)
        {
            this.numTopics = numTopics;
        }

        Task<PolicyDecision<int>> IContextMapper<UserContext, int>.MapContextAsync(UserContext context)
        {
            int chosenAction = (int)Math.Round(context.Features.Sum(f => f.Value) / context.Features.Count + 1);
            return Task.FromResult(PolicyDecision.Create(chosenAction));
        }
    }

    /// <summary>
    /// Represents the user context as a sparse vector of float features.
    /// </summary>
    public class UserContext 
    {
        public UserContext()
        {
            Features = new Dictionary<string, float>();
        }

        /// <summary>
        /// Gender of the user.
        /// </summary>
        public string Gender { get; set; }

        /// <summary>
        /// User features: in this case we assume that it is a generic map from properties to values.
        /// </summary>
        public Dictionary<string, float> Features { get; set; }
    }
}
