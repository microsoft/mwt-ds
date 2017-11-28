//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.DecisionService.Client;
using Microsoft.DecisionService.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.CustomDecisionService.RESTExample
{
    public static class Example1
    {
        public static void Run()
        {
            var client = new CustomDecisionServiceAPI();

            // 1. Sign in on https://ds.microsoft.com
            // 2. Create new app
            //   a.	Select "Custom App" 
            //   b. Supply Azure storage account
            //   c. Set Vowpal Wabbit arguments to --cb_explore_adf --epsilon 0.2 -q DT -q LT

            var appId = "<<CHANGE ME";

            Console.WriteLine($"Requesting decision for app '{appId}'...\n");
            var decisions = client.Rank(appId: appId,
                decisionRequests: new DecisionRequestCollection(new List<DecisionRequest>
                {
                    new DecisionRequest
                    {
                        Shared = new DecisionRequestShared
                        {
                            Features = new List<object>
                            {
                                new SharedContext
                                {
                                    Demographics = new DemographicNamespace
                                    {
                                        Gender = "female",
                                    },
                                    Location = new LocationNamespace
                                    {
                                        Country = "USA",
                                        City = "New York"
                                    }
                                }
                            }
                        },
                        Actions = new List<DecisionFeatures>
                        {
                            new DecisionFeatures
                            {
                                Ids = new List<DecisionReferenceId>
                                {
                                    new DecisionReferenceId(id: "1")
                                },
                                Features = new List<object>
                                {
                                    new ActionDependentFeatures
                                    {
                                        Topic = new TopicNamespace
                                        {
                                            Category = "Fashion"
                                        }
                                    }
                                }
                            },
                            new DecisionFeatures
                            {
                                Ids = new List<DecisionReferenceId>
                                {
                                    new DecisionReferenceId(id: "2")
                                },
                                Features = new List<object>
                                {
                                    new ActionDependentFeatures
                                    {
                                        Topic = new TopicNamespace
                                        {
                                            Category = "Technology"
                                        }
                                    }
                                }

                            }
                        }
                    }
                }));

            var decision = decisions[0];

            Console.WriteLine("EventId {0} Ranking: {1} RewardAction: {2}\n", 
                decision.EventId, 
                string.Join(",", decision.Ranking.Select(r => r.Id)),
                decision.RewardAction);

            client.Reward(decision.AppId, decision.EventId, 5f);

            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }
    }
}
