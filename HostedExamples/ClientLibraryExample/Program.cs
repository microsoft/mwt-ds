//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.CustomDecisionService.ClientLibraryExample;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using System;
using System.Threading.Tasks;
using VW.Serializer;

namespace ClientLibraryExample
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().Wait();


        }

        private static async Task MainAsync()
        {
            // Requires Visual C++ 2015 Runtime installed
            //  - https://www.microsoft.com/en-us/download/details.aspx?id=48145
            // Requires platform to be x64 

            // 1. Sign in on https://ds.microsoft.com
            // 2. Create new app
            //   a.	Select "Custom App" 
            //   b. Supply Azure storage account
            //   c. Empty Action Endpoint field - this disables action id based featurization
            //   d. Empty Action Set Endpoint field
            //   e. Set Provision FrontEnd field to "false" - avoids HTTP scoring provisioning
            //   f. Set Vowpal Wabbit arguments to --cb_explore_adf --epsilon 0.2 -q DT -q LT
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
            using (var ds = DecisionService.Create<DecisionContext>(config, typeInspector: JsonTypeInspector.Default))
            {
                // If you add the timestamp calculating reward latencies becomes very easy on our end
                // In general you can use existing request ids, just be careful as they're used to seed the random
                // generator for exploration.
                // Also they're used to join rank and reward calls, so they must be unique within the experimental unit duration
                var eventId = EventIdUtil.AddTimestamp(Guid.NewGuid().ToString("n"), EventIdUtil.BeginningOfTime);

                var context = new DecisionContext
                {
                    Demographics = new DemographicNamespace
                    {
                        Gender = "female",
                    },
                    Location = new LocationNamespace
                    {
                        Country = "USA",
                        City = "New York"
                    },
                    Actions = new[]
                    {
                        new ActionDependentFeatures
                        {
                            Topic = new TopicNamespace
                            {
                                Category = "Fashion"
                            }
                        },
                        new ActionDependentFeatures
                        {
                            Topic = new TopicNamespace
                            {
                                Category = "Technology"
                            }
                        }
                    }
                };

                // get the decision
                var ranking = await ds.ChooseRankingAsync(
                    uniqueKey: eventId,
                    context: context);

                // report the reward if you observed desired behavior on first item in ranking
                // e.g. the first item returned by ChooseRankingAsync got clicked.
                ds.ReportReward(reward: 1f, uniqueKey: eventId);
            }
        }
    }
}
