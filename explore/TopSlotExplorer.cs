using Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class TopSlotExplorer
    {
        // TODO: factory methods.
        private static TopSlotExplorer<TContext, TExplorer, TExplorerState, TPolicyState> 
            Create2<TContext, TExplorer, TExplorerState, TPolicyState>(IPolicy<TContext, uint[], TPolicyState> defaultPolicy,
            Func<IPolicy<TContext, uint, PolicyDecision<uint[], TPolicyState>>, TExplorer> singleExplorerFactory, 
            uint numActions)
                where TExplorer : IExplorer<TContext, uint, TExplorerState, PolicyDecision<uint[], TPolicyState>>, 
            IConsumePolicy<TContext, uint, PolicyDecision<uint[], TPolicyState>>
        {
            return new TopSlotExplorer<TContext, TExplorer, TExplorerState, TPolicyState>(defaultPolicy, singleExplorerFactory, numActions);
        }

        public static TopSlotExplorer<TContext, EpsilonGreedyExplorer<TContext, PolicyDecision<uint[], TPolicyState>>, EpsilonGreedyState, TPolicyState> 
            Create<TContext, TPolicyState>(
                IPolicy<TContext, uint[], TPolicyState> defaultPolicy, 
            float epsilon, 
            uint numActionsVariable = uint.MaxValue)
        {
            return Create2<TContext, EpsilonGreedyExplorer<TContext, PolicyDecision<uint[], TPolicyState> >, EpsilonGreedyState, TPolicyState>(
                defaultPolicy,
                policy => new EpsilonGreedyExplorer<TContext, PolicyDecision<uint[], TPolicyState>>(policy, epsilon, numActionsVariable), 
                numActionsVariable);
        }
    }

    public class TopSlotExplorer<TContext, TExplorer, TExplorerState, TPolicyState> 
        : BaseExplorer<TContext, uint[], TExplorerState, uint[], TPolicyState>
        where TExplorer : IExplorer<TContext, uint, TExplorerState, PolicyDecision<uint[], TPolicyState>>, 
            IConsumePolicy<TContext, uint, PolicyDecision<uint[], TPolicyState>>
    {
        private class TopSlotPolicy : IPolicy<TContext, uint, PolicyDecision<uint[], TPolicyState>>
        {
            private readonly IPolicy<TContext, uint[], TPolicyState> policy;

            internal TopSlotPolicy(IPolicy<TContext, uint[], TPolicyState> policy)
            {
                this.policy = policy;
            }
        
            public PolicyDecision<uint,PolicyDecision<uint[], TPolicyState>> ChooseAction(TContext context, uint numActionsVariable)
            {
                PolicyDecision<uint[], TPolicyState> policyDecision = this.policy.ChooseAction(context, numActionsVariable);

                return new PolicyDecision<uint, PolicyDecision<uint[], TPolicyState>>
                {
                    Action = policyDecision.Action[0],
                    PolicyState = policyDecision
                };
            }
        }

        private readonly TExplorer singleExplorer;

        public TopSlotExplorer(IPolicy<TContext, uint[], TPolicyState> defaultPolicy, 
            Func<IPolicy<TContext, uint, PolicyDecision<uint[], TPolicyState>>, TExplorer> singleExplorerFactory, 
            uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.singleExplorer = singleExplorerFactory(new TopSlotPolicy(defaultPolicy));
        }

        public virtual void UpdatePolicy(IPolicy<TContext, uint[], TPolicyState> newPolicy)
        {
            base.UpdatePolicy(newPolicy);
            singleExplorer.UpdatePolicy(new TopSlotPolicy(newPolicy));
        }

        protected override Decision<uint[], TExplorerState, TPolicyState> ChooseActionInternal(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            var decision = this.singleExplorer.ChooseAction(saltedSeed, context, numActionsVariable);

            var topAction = decision.Action;
            if (decision.PolicyDecision == null)
            {
                // TODO: execute policy and get actions...
            }

            var chosenActions = decision.PolicyDecision.PolicyState.Action;

            MultiActionHelper.PutActionToList(topAction, chosenActions);

            return Decision.Create(chosenActions, decision.ExplorerState, decision.PolicyDecision.PolicyState, true);
        }
    }
}
