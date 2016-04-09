using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public abstract class BaseExplorer<TValue, TMapperValue>
        : IExplorer<TValue, TMapperValue> 
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

        public abstract ExplorerDecision<TValue> MapContext(ulong saltedSeed, TMapperValue policyAction);
    }


    public abstract class BaseVariableActionExplorer<TValue, TMapperValue>
       : BaseExplorer<TValue, TMapperValue>, IVariableActionExplorer<TValue, TMapperValue>
    {
        // TODO: change int.max to nullable
        protected BaseVariableActionExplorer(int numActions = int.MaxValue)
            : base(numActions) { }

        public override ExplorerDecision<TValue> MapContext(ulong saltedSeed, TMapperValue policyAction)
        {
            return this.Explore(saltedSeed, policyAction, this.numActionsFixed);
        }

        public abstract ExplorerDecision<TValue> Explore(ulong saltedSeed, TMapperValue policyAction, int numActionsVariable);
    }
}
