using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public abstract class BaseExplorer<TContext, TAction, TExplorerState, TPolicyAction, TPolicyState>
        : IExplorer<TContext, TAction, TExplorerState, TPolicyAction, TPolicyState>, IConsumePolicy<TContext, TPolicyAction, TPolicyState>
    {
        protected IContextMapper<TContext, TPolicyAction, TPolicyState> defaultPolicy;
        protected bool explore;
        protected readonly uint numActionsFixed;

        protected BaseExplorer(IContextMapper<TContext, TPolicyAction, TPolicyState> defaultPolicy, uint numActions = uint.MaxValue)
        {
            if (defaultPolicy == null)
                throw new ArgumentNullException("defaultPolicy");

            VariableActionHelper.ValidateInitialNumberOfActions(numActions);

            this.defaultPolicy = defaultPolicy;
            this.explore = true;
            this.numActionsFixed = numActions;
        }

        public virtual void UpdatePolicy(IContextMapper<TContext, TPolicyAction, TPolicyState> newPolicy)
        {
            this.defaultPolicy = newPolicy;
        }

        public virtual void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public Decision<TAction, TExplorerState, TPolicyAction, TPolicyState> MapContext(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            uint numActions = (numActionsFixed == uint.MaxValue) ? numActionsVariable : numActionsFixed;

            // Actual number of actions at decision time must be a valid positive finite number.
            if (numActions == uint.MaxValue || numActions < 1)
            {
                throw new ArgumentException("Number of actions must be at least 1.");
            }

            return this.MapContextInternal(saltedSeed, context, numActions);
        }

        protected abstract Decision<TAction, TExplorerState, TPolicyAction, TPolicyState> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable);
    }
}
