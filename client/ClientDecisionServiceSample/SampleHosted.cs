using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VW.Serializer;

namespace ClientDecisionServiceSample
{
    public static class SampleHosted
    {
        public static async Task Sample()
        {
            // 1. Sign in on https://ds.microsoft.com
            // 2. Create new app
            //   a.	Select “Custom App”
            //   b. Supply Azure storage account
            //   c. Empty Action Endpoint field - this disables action id based featurization
            //   d. Empty Action Set Endpoint field
            //   e. Set Provision FrontEnd field to “false” - avoids HTTP scoring provisioning
            //   f. Set Vowpal Wabbit arguments to --cb_explore_adf --epsilon 0.2 -q gc -q gC -q dC
            //   f. Click create
            // 3. Wait for creation and hit edit
            //   a. Copy Client Library URL and use in the sample code below
            // 4. nuget install Microsoft.Research.MultiWorldTesting.ClientLibrary
            //   a. at least version 2.0.0.6

            var config = new DecisionServiceConfiguration(settingsBlobUri: "<<INSERT URL FROM STEP 3A")
            {
                ModelPollFailureCallback = ex => Console.WriteLine(ex.Message),
                SettingsPollFailureCallback = ex => Console.WriteLine(ex.Message)
            };

            // you should keep this object around (e.g. static variable) as it maintains the background process 
            // to download new models and upload logged events
            var ds = DecisionService.Create<MyDecisionContext>(config, typeInspector: JsonTypeInspector.Default);

            // Some sample data
            var context = new MyDecisionContext
            {
                Demographics = new MyDecisionContextDemographics
                {
                    Age = 15,
                    AgeGroup = "Teenager",
                    Gender = "male"
                },
                Geo = new MyDecisionContextGeo
                {
                    City = "New York",
                    Country = "USA"
                },
                Actions = new[]
                {
                    new MyDecisionContextAction
                    {
                        Categories = new MyDecisionContextActionCategorization { Categories = "Fashion OldSchool" },
                        Celebrities = new Dictionary<string, float>
                        {
                            { "Markus", 0.9f },
                            { "Bob", 0.1f }
                        }
                    },
                    new MyDecisionContextAction
                    {
                        Categories = new MyDecisionContextActionCategorization { Categories = "Tech Microsoft" },
                        Celebrities = new Dictionary<string, float>
                        {
                            { "Bill", 0.7f },
                            { "Bob", 0.2f }
                        }
                    }
                }
            };

            for (int i = 0; i < 5; i++)
            {
                // this is used as both correlation event id to link the ranking request to the observerd outcome
                // as well as random seed for the pseudo random generator
                var eventId = Guid.NewGuid().ToString("n");

                // get the decision
                var ranking = await ds.ChooseRankingAsync(
                    uniqueKey: eventId,
                    context: context);

                // report the reward if you observed desired behavior on first item in ranking
                // e.g. the first item returned by ChooseRankingAsync got clicked.
                ds.ReportReward(reward: 1f, uniqueKey: eventId);
            }

            Console.WriteLine("done");

            Thread.Sleep(TimeSpan.FromHours(1));

            Console.WriteLine();
        }

        public class MyDecisionContext
        {
            /// <summary>
            /// Renamed to "g" to select Vowpal Wabbit namespace 'g' 
            /// </summary>
            [JsonProperty("geo")]
            public MyDecisionContextGeo Geo { get; set; }

            [JsonProperty("demo")]
            public MyDecisionContextDemographics Demographics { get; set; }

            /// <summary>
            /// The action array must be annotated as _multi property.
            /// </summary>
            [JsonProperty("_multi")]
            public MyDecisionContextAction[] Actions { get; set; }
        }

        public class MyDecisionContextGeo
        {
            public string City { get; set; }

            public string Country { get; set; }
        }

        public class MyDecisionContextDemographics
        {
            public string Gender { get; set; }

            /// <summary>
            /// Note that string and not float is used as you want to get individual weights for each age group.
            /// </summary>
            public string AgeGroup { get; set; }

            /// <summary>
            /// Properties starting with _ are ignored from ML. Just include to enable offline experimentation.
            /// </summary>
            [JsonProperty("_age")]
            public int Age { get; set; }
        }

        /// <summary>
        /// This can represent a document, video, banner or a setting. 
        /// </summary>
        public class MyDecisionContextAction
        {
            [JsonProperty("cat")]
            public MyDecisionContextActionCategorization Categories { get; set; }

            [JsonProperty("Celeb")]
            public Dictionary<string, float> Celebrities { get; set; }
        }

        public class MyDecisionContextActionCategorization
        {
            [JsonProperty("_text")]
            public string Categories { get; set; }
        }
    }
}
