using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public abstract class BaseExplorer<TContext, TValue, TExplorerState, TPolicyValue, TMapperState>
        : IExplorer<TContext, TValue, TExplorerState, TPolicyValue, TMapperState>, IConsumeContextMapper<TContext, TPolicyValue, TMapperState>
    {
        protected IContextMapper<TContext, TPolicyValue, TMapperState> defaultPolicy;
        protected bool explore;
        protected readonly uint numActionsFixed;

        protected BaseExplorer(IContextMapper<TContext, TPolicyValue, TMapperState> defaultPolicy, uint numActions = uint.MaxValue)
        {
            if (defaultPolicy == null)
                throw new ArgumentNullException("defaultPolicy");

            VariableActionHelper.ValidateInitialNumberOfActions(numActions);

            this.defaultPolicy = defaultPolicy;
            this.explore = true;
            this.numActionsFixed = numActions;
        }

        public virtual void UpdatePolicy(IContextMapper<TContext, TPolicyValue, TMapperState> newPolicy)
        {
            this.defaultPolicy = newPolicy;
        }

        public virtual void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public Decision<TValue, TExplorerState, TPolicyValue, TMapperState> MapContext(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            uint numActions = (numActionsFixed == uint.MaxValue) ? numActionsVariable : numActionsFixed;

            // Actual number of actions at decision time must be a valid positive finite number.
            if (numActions == uint.MaxValue || numActions < 1)
            {
                throw new ArgumentException("Number of actions must be at least 1.");
            }

            return this.MapContextInternal(saltedSeed, context, numActions);
        }

        protected abstract Decision<TValue, TExplorerState, TPolicyValue, TMapperState> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable);
    }
}
