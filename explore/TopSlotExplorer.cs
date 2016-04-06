using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public class TopSlotExplorer<TContext, TExplorer, TExplorerState> : BaseExplorer<TContext, uint[], TExplorerState, uint[]>
        where TExplorer : IVariableActionExplorer<TContext, uint, TExplorerState, uint>
    {
        private class TopSlotPolicy : IPolicy<TContext>
        {
            private readonly IContextMapper<TContext, uint[]> policy;
            
            // TODO: review if we can remove this threadloacl
            private ThreadLocal<uint> action;

            internal TopSlotPolicy(IContextMapper<TContext, uint[]> policy)
            {
                this.policy = policy;
            }

            public void UpdateAction(uint action)
            {
                this.action.Value = action;
            }

            public Decision<uint> MapContext(TContext context)
            {
                return this.action.Value;
                //Decision<uint[]> policyDecision = this.policy.MapContext(context);

                ////numActionsVariable = (uint)policyDecision.Value.Length;

                //return Decision.Create(policyDecision.Value[0], policyDecision);
            }
        }

        private readonly TExplorer singleExplorer;
        private readonly TopSlotPolicy topSlotPolicy;
        private readonly IContextMapper<TContext, uint[]> policy;

        public TopSlotExplorer(IContextMapper<TContext, uint[]> defaultPolicy,
            Func<IPolicy<TContext>, TExplorer> singleExplorerFactory, 
            uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.singleExplorer = singleExplorerFactory(new TopSlotPolicy(defaultPolicy));
        }

        public override Decision<uint[], TExplorerState, uint[]> MapContext(ulong saltedSeed, TContext context)
        {
            var policyDecision = this.policy.MapContext(context);
            // TOdO: check if the Value is empty or null array
            this.topSlotPolicy.UpdateAction(policyDecision.Value[0]);
            var decision = this.singleExplorer.MapContext(saltedSeed, context, (uint)policyDecision.Value.Length);
            MultiActionHelper.PutActionToList(decision.Value, policyDecision.Value);

            return Decision.Create(policyDecision.Value, decision.ExplorerState, policyDecision, decision.ShouldRecord);
        }
    }
}