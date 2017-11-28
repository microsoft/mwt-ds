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
using System.Threading;

namespace Microsoft.CustomDecisionService.RESTExample
{
    public static class Example2
    {
        private static DecisionRequestCollection CreateDecisionRequestCollection(string gender)
        {
            return new DecisionRequestCollection(new List<DecisionRequest>
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
                                        Gender = gender,
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
            });
        }

        private static void PrintDecisionServiceBehavior(this Dictionary<string, Dictionary<string, int>> distributionOverActionsPerContext)
        {
            Console.WriteLine();
            Console.WriteLine(DateTime.Now);

            var requests = distributionOverActionsPerContext.Sum(d => d.Value.Sum(action => action.Value));
            foreach (var gender in distributionOverActionsPerContext)
            {
                Console.WriteLine($"Gender: {gender.Key}");
                foreach (var action in gender.Value)
                    Console.WriteLine($"\tAction {action.Key}: {action.Value,6} {action.Value/(float)requests:0.00}");
            }

            Console.WriteLine();
        }


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

            var random = new Random(42);

            // simulation parameterization
            var personas = new[] {
                new { gender = "male",   probabilityOfChoosingAction1 = 1.0 },
                new { gender = "female", probabilityOfChoosingAction1 = 0.0 }
             };

            // gender (male/female) -> action (1/2) -> count (0)
            var distributionOverActionsPerContext = personas.ToDictionary(
                p => p.gender,
                p => CreateDecisionRequestCollection(p.gender).Decisions[0].Actions.ToDictionary(a => a.Ids.First().Id, _ => 0));

            var requests = 0;
            for (int i = 0; true; i++)
            {
                // alternate between personas
                var persona = personas[i % personas.Length];

                // generate context
                var decisionRequests = CreateDecisionRequestCollection(persona.gender);

                // get decision
                Console.Write(".");
                // automatically retries on failure
                var decision = client.Rank(appId, decisionRequests)[0];

                // simulate stochastic behavior
                var probabilityOfRewardingAction =
                    decision.RewardAction == "1" ?
                    persona.probabilityOfChoosingAction1 : 1 - persona.probabilityOfChoosingAction1;

                if (random.NextDouble() < probabilityOfRewardingAction)
                    client.Reward(appId, decision.EventId, 1f);
                
                // build up stats
                distributionOverActionsPerContext[persona.gender][decision.RewardAction]++;
                requests++;

                if (requests % 50 == 0)
                    distributionOverActionsPerContext.PrintDecisionServiceBehavior();

                // 10 req / second
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }
        }
    }
}
