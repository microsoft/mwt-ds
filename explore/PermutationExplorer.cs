using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public sealed class PermutationExplorer : IFullExplorer<int[]>
    {
        private readonly int maxPermutations;

        public PermutationExplorer(int maxPermutations = int.MaxValue)
        {
            this.maxPermutations = maxPermutations;
        }

        public ExplorerDecision<int[]> Explore(PRG random, int numActionsVariable)
        {
            var ranking = Enumerable.Range(1, numActionsVariable).ToArray();

            for (int i = 0; i < ranking.Length - 1 && i < maxPermutations; i++)
            {
                int swapIndex = random.UniformInt(i, ranking.Length - 1);

                int temp = ranking[swapIndex];
                ranking[swapIndex] = ranking[i];
                ranking[i] = temp;
            }

            return ExplorerDecision.Create(ranking, new GenericExplorerState { Probability = 1f }, true);
        }
    }
}
