using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public sealed class PermutationExplorer<TContext> : IFullExplorer<TContext, int[]>
    {
        private readonly INumberOfActionsProvider<TContext> numberOfActionsProvider;
        private readonly int maxPermutations;

        public PermutationExplorer(INumberOfActionsProvider<TContext> numberOfActionsProvider, int maxPermutations = int.MaxValue)
        {
            if (numberOfActionsProvider == null)
                throw new ArgumentNullException("numberOfActionsProvider");

            this.numberOfActionsProvider = numberOfActionsProvider;
            this.maxPermutations = maxPermutations;
        }

        public ExplorerDecision<int[]> Explore(ulong saltedSeed, TContext context)
        {
            var numActionsVariable = this.numberOfActionsProvider.GetNumberOfActions(context);

            var ranking = Enumerable.Range(1, numActionsVariable).ToArray();
            var random = new PRG(saltedSeed);

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
