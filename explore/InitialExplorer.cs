using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class InitialExplorer
    {
        private static readonly IInitialExplorer<ActionProbability[], int[]> uniformRandomInitialExplorer = new UniformRandomInitialExplorer();

        public static IInitialExplorer<ActionProbability[], int[]> Create(float epsilon)
        {
            return epsilon == 1f ? uniformRandomInitialExplorer : new EpsilonGreedyInitialExplorer(epsilon);
        }

        private sealed class EpsilonGreedyInitialExplorer : IInitialExplorer<ActionProbability[], int[]>
        {
            private float epsilon;

            public EpsilonGreedyInitialExplorer(float epsilon)
            {
                this.epsilon = epsilon;
            }

            //public IEnumerable<ActionProbability> Explore(int defaultValue, int numActions)
            //{
            //    float prob = this.epsilon / numActions;

            //    return Enumerable.Range(1, numActions)
            //        .Select(action => 
            //            new ActionProbability 
            //            { 
            //                Action = action, 
            //                Probability = action == defaultValue ? prob + (1 - this.epsilon) : prob
            //            });
            //}

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

        private sealed class UniformRandomInitialExplorer : IInitialExplorer<ActionProbability[], int[]>
        {
            //public IEnumerable<ActionProbability> Explore(int defaultValue, int numActions)
            //{
            //    var prob = 1f / numActions;
            //    return Enumerable.Range(1, numActions)
            //        .Select(i => new ActionProbability { Action = i, Probability = prob });
            //}

            public ActionProbability[] Explore(int[] defaultValues)
            {
                float prob = 1f / defaultValues.Length;
                return Enumerable.Range(1, defaultValues.Length)
                    .Select(i => new ActionProbability { Action = i, Probability = prob })
                    .ToArray();
            }
        }
    }
}
