using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public abstract class BaseExplorer<TAction, TPolicyValue>
        : IExplorer<TAction, TPolicyValue>
    {
        protected bool explore;

        protected BaseExplorer()
        {
            this.explore = true;
        }

        public virtual void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public abstract ExplorerDecision<TAction> MapContext(PRG prg, TPolicyValue policyAction, int numActions);
    }
}
