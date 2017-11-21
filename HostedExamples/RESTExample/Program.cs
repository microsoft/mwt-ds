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

            // TODO
            // 1. Create a new app on https://ds.microsoft.com
            // 2. Change app1 to your app id
            var decision = client.Rank(appId: "app1",
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
