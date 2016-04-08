using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using VW.Interfaces;
using VW.Labels;
using VW.Serializer.Attributes;

namespace ClientDecisionServiceSample
{
    public class ADFContext
    {
        [Feature]
        public string[] Shared { get; set; }

        public IReadOnlyList<ADFFeatures> ActionDependentFeatures { get; set; }

        public string ModelId { get; set; }

        public static ADFContext CreateRandom(int numActions, Random rg)
        {
            int iCB = rg.Next(0, numActions);

            var fv = new ADFFeatures[numActions];
            for (int i = 0; i < numActions; i++)
            {
                fv[i] = new ADFFeatures
                {
                    Features = new[] { "a_" + (i + 1), "b_" + (i + 1), "c_" + (i + 1) }
                };

                if (i == iCB) // Randomly place a Contextual Bandit label
                {
                    fv[i].Label = new ContextualBanditLabel
                    {
                        Cost = (float)rg.NextDouble(),
                        Probability = (float)rg.NextDouble()
                    };
                }
            }

            var context = new ADFContext
            {
                Shared = new string[] { "shared", "features" },
                ActionDependentFeatures = fv
            };
            return context;
        }
    }

    public class ADFFeatures
    {
        [Feature]
        public string[] Features { get; set; }

        public override string ToString()
        {
            return string.Join(" ", this.Features);
        }

        public ILabel Label { get; set; }
    }

    class ADFPolicy : IRanker<ADFContext>
    {
        public Decision<int[]> MapContext(ADFContext context)
        {
            return Enumerable.Range(1, context.ActionDependentFeatures.Count).ToArray();
        }
    }
}
