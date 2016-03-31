using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public class TopSlotExplorer<TContext, TExplorer, TExplorerState> : BaseExplorer<TContext, uint[], TExplorerState, uint[]>
        where TExplorer : IExplorer<TContext, uint, TExplorerState, uint>
    {
        private class TopSlotPolicy : IPolicy<TContext>
        {
            private readonly IContextMapper<TContext, uint[]> policy;

            internal TopSlotPolicy(IContextMapper<TContext, uint[]> policy)
            {
                this.policy = policy;
            }
        
            public Decision<uint> MapContext(TContext context, ref uint numActionsVariable)
            {
                Decision<uint[]> policyDecision = this.policy.MapContext(context, ref numActionsVariable);

                numActionsVariable = (uint)policyDecision.Value.Length;

                return Decision.Create(policyDecision.Value[0], policyDecision);
            }
        }

        private readonly TExplorer singleExplorer;

        public TopSlotExplorer(IContextMapper<TContext, uint[]> defaultPolicy,
            Func<IPolicy<TContext>, TExplorer> singleExplorerFactory, 
            uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.singleExplorer = singleExplorerFactory(new TopSlotPolicy(defaultPolicy));
        }

        protected override Decision<uint[], TExplorerState, uint[]> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            var decision = this.singleExplorer.MapContext(saltedSeed, context, numActionsVariable);

            var policyDecision = (Decision<uint[]>)decision.MapperDecision.MapperState;
            if (policyDecision == null)
            {
                // for TauFirst the policy doesn't get executed the first tau times.
                policyDecision = this.contextMapper.MapContext(context, ref numActionsVariable);
            }

            var topAction = decision.Value;
            var chosenActions = policyDecision.Value;

            MultiActionHelper.PutActionToList(topAction, chosenActions);

            return Decision.Create(chosenActions, decision.ExplorerState, policyDecision, decision.ShouldRecord);
        }
    }
}