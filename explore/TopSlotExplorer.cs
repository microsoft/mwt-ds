using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class TopSlotExplorer
    {
        private static TopSlotExplorer<TContext, TExplorer, TExplorerState, TPolicyState> 
            Create<TContext, TExplorer, TExplorerState, TPolicyState>(IRanker<TContext, TPolicyState> defaultPolicy,
            Func<IPolicy<TContext, PolicyDecision<uint[], TPolicyState>>, TExplorer> singleExplorerFactory, 
            uint numActions)
                where TExplorer : IExplorer<TContext, uint, TExplorerState, uint, PolicyDecision<uint[], TPolicyState>>, 
            IConsumePolicy<TContext, uint, PolicyDecision<uint[], TPolicyState>>
        {
            return new TopSlotExplorer<TContext, TExplorer, TExplorerState, TPolicyState>(defaultPolicy, singleExplorerFactory, numActions);
        }

        public static TopSlotExplorer<TContext, EpsilonGreedyExplorer<TContext, PolicyDecision<uint[], TPolicyState>>, EpsilonGreedyState, TPolicyState>
            CreateEpsilonGreedyExplorer<TContext, TPolicyState>(IRanker<TContext, TPolicyState> defaultPolicy, float epsilon, uint numActionsVariable = uint.MaxValue)
        {
            return Create<TContext, EpsilonGreedyExplorer<TContext, PolicyDecision<uint[], TPolicyState>>, EpsilonGreedyState, TPolicyState>(
                defaultPolicy,
                policy => new EpsilonGreedyExplorer<TContext, PolicyDecision<uint[], TPolicyState>>(policy, epsilon, numActionsVariable), 
                numActionsVariable);
        }

        public static TopSlotExplorer<TContext, TauFirstExplorer<TContext, PolicyDecision<uint[], TPolicyState>>, TauFirstState, TPolicyState>
            CreateTauFirstExplorer<TContext, TPolicyState>(IRanker<TContext, TPolicyState> defaultPolicy, uint tau, uint numActionsVariable = uint.MaxValue)
        {
            return Create<TContext, TauFirstExplorer<TContext, PolicyDecision<uint[], TPolicyState>>, TauFirstState, TPolicyState>(
                defaultPolicy,
                policy => new TauFirstExplorer<TContext, PolicyDecision<uint[], TPolicyState>>(policy, tau, numActionsVariable),
                numActionsVariable);
        }
    }

    public class TopSlotExplorer<TContext, TExplorer, TExplorerState, TPolicyState> 
        : BaseExplorer<TContext, uint[], TExplorerState, uint[], TPolicyState>
        where TExplorer : IExplorer<TContext, uint, TExplorerState, uint, PolicyDecision<uint[], TPolicyState>>, 
            IConsumePolicy<TContext, uint, PolicyDecision<uint[], TPolicyState>>
    {
        private class TopSlotPolicy : IPolicy<TContext, PolicyDecision<uint[], TPolicyState>>
        {
            private readonly IContextMapper<TContext, uint[], TPolicyState> policy;

            internal TopSlotPolicy(IContextMapper<TContext, uint[], TPolicyState> policy)
            {
                this.policy = policy;
            }
        
            public PolicyDecision<uint,PolicyDecision<uint[], TPolicyState>> MapContext(TContext context, uint numActionsVariable)
            {
                PolicyDecision<uint[], TPolicyState> policyDecision = this.policy.MapContext(context, numActionsVariable);

                return new PolicyDecision<uint, PolicyDecision<uint[], TPolicyState>>
                {
                    Action = policyDecision.Action[0],
                    PolicyState = policyDecision
                };
            }
        }

        private readonly TExplorer singleExplorer;

        public TopSlotExplorer(IRanker<TContext, TPolicyState> defaultPolicy, 
            Func<IPolicy<TContext, PolicyDecision<uint[], TPolicyState>>, TExplorer> singleExplorerFactory, 
            uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.singleExplorer = singleExplorerFactory(new TopSlotPolicy(defaultPolicy));
        }

        public override void UpdatePolicy(IContextMapper<TContext, uint[], TPolicyState> newPolicy)
        {
            base.UpdatePolicy(newPolicy);
            singleExplorer.UpdatePolicy(new TopSlotPolicy(newPolicy));
        }

        protected override Decision<uint[], TExplorerState, uint[], TPolicyState> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            var decision = this.singleExplorer.MapContext(saltedSeed, context, numActionsVariable);

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
