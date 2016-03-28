using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public class TopSlotExplorer<TContext, TExplorer, TExplorerState, TMapperState> : BaseExplorer<TContext, uint[], TExplorerState, uint[], TMapperState>
        where TExplorer : IExplorer<TContext, uint, TExplorerState, uint, Decision<uint[], TMapperState>>, 
            IConsumeContextMapper<TContext, uint, Decision<uint[], TMapperState>>
    {
        private class TopSlotPolicy : IPolicy<TContext, Decision<uint[], TMapperState>>
        {
            private readonly IContextMapper<TContext, uint[], TMapperState> policy;

            internal TopSlotPolicy(IContextMapper<TContext, uint[], TMapperState> policy)
            {
                this.policy = policy;
            }
        
            public Decision<uint,Decision<uint[], TMapperState>> MapContext(TContext context, uint numActionsVariable)
            {
                Decision<uint[], TMapperState> policyDecision = this.policy.MapContext(context, numActionsVariable);

                return new Decision<uint, Decision<uint[], TMapperState>>
                {
                    Value = policyDecision.Value[0],
                    MapperState = policyDecision
                };
            }
        }

        private readonly TExplorer singleExplorer;

        public TopSlotExplorer(IRanker<TContext, TMapperState> defaultPolicy, 
            Func<IPolicy<TContext, Decision<uint[], TMapperState>>, TExplorer> singleExplorerFactory, 
            uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.singleExplorer = singleExplorerFactory(new TopSlotPolicy(defaultPolicy));
        }

        public override void UpdatePolicy(IContextMapper<TContext, uint[], TMapperState> newPolicy)
        {
            base.UpdatePolicy(newPolicy);
            singleExplorer.UpdatePolicy(new TopSlotPolicy(newPolicy));
        }

        protected override Decision<uint[], TExplorerState, uint[], TMapperState> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            var decision = this.singleExplorer.MapContext(saltedSeed, context, numActionsVariable);

            var policyDecision = decision.MapperDecision.MapperState;
            if (policyDecision == null)
            {
                // for TauFirst the policy doesn't get executed the first tau times.
                policyDecision = this.defaultPolicy.MapContext(context, numActionsVariable);
            }

            var topAction = decision.Value;
            var chosenActions = policyDecision.Value;

            MultiActionHelper.PutActionToList(topAction, chosenActions);

            return Decision.Create(chosenActions, decision.ExplorerState, policyDecision, decision.ShouldRecord);
        }
    }
}