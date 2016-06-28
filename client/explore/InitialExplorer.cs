using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public sealed class EpsilonGreedyInitialExplorer : IInitialExplorer<ActionProbability[], int[]>
    {
        private float epsilon;

        public EpsilonGreedyInitialExplorer(float epsilon)
        {
            this.epsilon = epsilon;
        }

        public ActionProbability[] Explore(int[] defaultValues)
        {
            float prob = this.epsilon / defaultValues.Length;
            return defaultValues
                .Select(action =>
                    new ActionProbability
                    {
                        Action = action,
                        Probability = action == defaultValues[0] ? prob + (1 - this.epsilon) : prob
                    })
                .ToArray();
        }
    }
}
