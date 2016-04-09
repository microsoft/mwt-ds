using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{

    // IFullExplorer<int, MyMagicType> foo = new UniformRandomExploration();

    public sealed class UniformRandomExploration : IFullExplorer<object, int>
    {
        private int numActions;

        public UniformRandomExploration(int numActions)
        {
            this.numActions = numActions;
        }

        public ExplorerDecision<int> Explore(ulong saltedSeed, object dummy)
        {
            return ExplorerDecision.Create(
                 new PRG(saltedSeed).UniformInt(1, numActions),
                 new GenericExplorerState { Probability = 1f },
                 shouldRecord: true);
        }
    }
}
