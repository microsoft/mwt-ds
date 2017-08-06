using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VW;

namespace ClientDecisionServiceSample
{
    public static class SampleActionDependentFeature
    {
        /***** Copy & Paste your application settings URL here *****
         ***** This value can either be found after deployment *****
         ***** or in the Management Center of Decision Service *****/
        static readonly string SettingsBlobUri = "";

        /// <summary>
        /// Sample code simulating a news recommendation scenario. In this simple example, 
        /// the rendering server has to pick 1 out of 4 news topics to show to users (e.g. as featured article).
        /// </summary>
        /// <remarks>
        /// NOTE: For this sample to work, the proper settings must be set at deployment time:
        /// Vowpal Wabbit Switches = --cb_explore_adf --epsilon 0.2 --cb_type dr
        /// </remarks>
        public static async Task NewsRecommendation()
        {
            if (String.IsNullOrWhiteSpace(SettingsBlobUri))
            {
                Console.WriteLine("Please specify a valid settings URL.");
                return;
            }

            // Create configuration for the decision service
            var serviceConfig = new DecisionServiceConfiguration(settingsBlobUri: SettingsBlobUri);

            // Enable development mode to easily debug / diagnose data flow and system properties
            // This should be turned off in production deployment
            serviceConfig.DevelopmentMode = true;

            using (var service = DecisionService.Create<UserContextADF>(serviceConfig))
            {
                var random = new Random();
                int user = 0; // user id 
                int maxDecisionHistory = 100; // max number of past decisions to record
                var correctDecisions = new Queue<int>(); // keeps track of past decisions

                Console.WriteLine("Press Ctrl + C at any time to cancel the process.");

                // Each topic has its own set of features
                var topicFeatures = Enumerable.Range(1, 10)
                    .Select(_ => new TopicFeature 
                    { 
                        Features = Enumerable.Range(1, 10).Select(f => (float)random.NextDouble()).ToArray() 
                    })
                    .ToArray();

                while (true)
                {
                    user++;

                    string uniqueKey = Guid.NewGuid().ToString();

                    var userContext = new UserContextADF 
                    { 
                        Gender = random.NextDouble() > 0.5 ? "male" : "female",
                        TopicFeatures = topicFeatures.Take(random.Next(5, topicFeatures.Length)).ToArray()
                    };
                    int[] topicRanking = await service.ChooseRankingAsync(uniqueKey, userContext);

                    int topicId = topicRanking[0];

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

    public class UserContextADF
    {
        public string Gender { get; set; }

        // Use Json annotation to mark this as multi-line features
        [JsonProperty(PropertyName = "_multi")]
        public TopicFeature[] TopicFeatures { get; set; }

        public static IReadOnlyCollection<TopicFeature> GetFeaturesFromContext(UserContextADF context)
        {
            return context.TopicFeatures;
        }
    }

    public class TopicFeature
    {
        public float[] Features { get; set; }
    }

}
