using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    // TODO: what about removing this class entirely and refactor the Top Slot logic as static methods instead?
    public sealed class TopSlotExplorer : BaseExplorer<int[], int[]>
    {
        private readonly IExplorer<int, int> explorer;

        public TopSlotExplorer(IExplorer<int, int> explorer)
        {
            if (explorer == null)
                throw new ArgumentNullException("variableActionExplorer");

            this.explorer = explorer;
        }

        public override ExplorerDecision<int[]> MapContext(PRG prg, int[] ranking, int numActions)
        {
            if (ranking == null || ranking.Length < 1)
                throw new ArgumentException("Actions chosen by default policy must not be empty.");

            var decision = this.explorer.MapContext(prg, ranking[0], ranking.Length);
            MultiActionHelper.PutActionToList(decision.Value, ranking);

            return ExplorerDecision.Create(ranking, decision.ExplorerState, decision.ShouldRecord);
        }
    }
}