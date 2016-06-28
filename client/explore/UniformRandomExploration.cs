using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{

    // IFullExplorer<int> foo = new UniformRandomExploration();

    public sealed class UniformRandomExploration : IFullExplorer<int>
    {
        public ExplorerDecision<int> Explore(PRG random, int numActions)
        {
            return ExplorerDecision.Create(
                 random.UniformInt(1, numActions),
                 new GenericExplorerState { Probability = 1f },
                 shouldRecord: true);
        }
    }
}
