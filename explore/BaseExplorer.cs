using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public abstract class BaseExplorer<TAction, TPolicyValue>
        : IExplorer<TAction, TPolicyValue>, INumberOfActionsProvider<object>
    {
        protected bool explore;
        protected readonly int numActionsFixed;

        protected BaseExplorer(int numActions)
        {
            VariableActionHelper.ValidateInitialNumberOfActions(numActions);

            this.explore = true;
            this.numActionsFixed = numActions;
        }

        public virtual void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public int GetNumberOfActions(object context)
        {
            return this.numActionsFixed;
        }

        public abstract ExplorerDecision<TAction> MapContext(PRG prg, TPolicyValue policyAction);
    }


    public abstract class BaseVariableActionExplorer<TAction, TPolicyValue>
       : BaseExplorer<TAction, TPolicyValue>, IVariableActionExplorer<TAction, TPolicyValue>
    {
        // TODO: change int.max to nullable
        protected BaseVariableActionExplorer(int numActions = int.MaxValue)
            : base(numActions) { }

        public override ExplorerDecision<TAction> MapContext(PRG prg, TPolicyValue policyAction)
        {
            return this.Explore(prg, policyAction, this.numActionsFixed);
        }

        public abstract ExplorerDecision<TAction> Explore(PRG prg, TPolicyValue policyAction, int numActionsVariable);
    }
}
