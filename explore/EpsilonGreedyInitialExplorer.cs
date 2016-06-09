using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public sealed class EpsilonGreedyInitialExplorer
    {
        private float epsilon;

        public EpsilonGreedyInitialExplorer(float epsilon)
        {
            this.epsilon = epsilon;
        }

        public ActionProbability[] Explore(int defaultValue, int numActions)
        {
            float prob;

            // uniform random
            if (this.epsilon == 1f)
            {
                prob = 1f / numActions;
                return Enumerable.Range(1, numActions)
                    .Select(i => new ActionProbability { Action = i, Probability = prob })
                    .ToArray();
            }

            prob = this.epsilon / numActions;
            var actionProbs = Enumerable.Range(1, numActions)
                .Select(action => new ActionProbability { Action = action, Probability = prob })
                .ToArray();

            actionProbs[defaultValue].Probability += 1 - this.epsilon;

            return actionProbs;
        }

        public ActionProbability[] Explore(int[] defaultValues)
        {
            float prob;

            // uniform random
            if (this.epsilon == 1f)
            {
                prob = 1f / defaultValues.Length;
                return Enumerable.Range(1, defaultValues.Length)
                    .Select(i => new ActionProbability { Action = i, Probability = prob })
                    .ToArray();
            }

            prob = this.epsilon / defaultValues.Length;
            var actionProbs = defaultValues
                .Select(action => new ActionProbability { Action = action, Probability = prob })
                .ToArray();

            actionProbs[defaultValues[0]].Probability += 1 - this.epsilon;

            return actionProbs;
        }
    }
}
