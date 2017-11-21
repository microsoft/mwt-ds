//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.DecisionService.Client;
using Microsoft.DecisionService.Client.Models;
using System.Collections.Generic;

namespace Microsoft.CustomDecisionService.RESTExample
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new CustomDecisionServiceAPI();

            // 1. Sign in on https://ds.microsoft.com
            // 2. Create new app
            //   a.	Select "Custom App" 
            //   b. Supply Azure storage account
            //   c. Set Vowpal Wabbit arguments to --cb_explore_adf --epsilon 0.2 -q DT -q LT

            var decision = client.Rank(appId: "<<INSERT appId",
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

            client.Reward(decision[0].AppId, decision[0].EventId, 5f);
        }
    }
}
