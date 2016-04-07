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
            private ThreadLocal<uint> action = new ThreadLocal<uint>();

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
            }
        }

        private readonly TExplorer singleExplorer;
        private readonly TopSlotPolicy topSlotPolicy;

        public TopSlotExplorer(IContextMapper<TContext, uint[]> defaultPolicy,
            Func<IPolicy<TContext>, TExplorer> singleExplorerFactory, 
            uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.topSlotPolicy = new TopSlotPolicy(defaultPolicy);
            this.singleExplorer = singleExplorerFactory(this.topSlotPolicy);
        }

        public override Decision<uint[], TExplorerState, uint[]> MapContext(ulong saltedSeed, TContext context)
        {
            var policyDecision = this.contextMapper.MapContext(context);
            if (policyDecision.Value == null || policyDecision.Value.Length < 1)
            {
                throw new ArgumentException("Actions chosen by default policy must not be empty.");
            }
            this.topSlotPolicy.UpdateAction(policyDecision.Value[0]);
            var decision = this.singleExplorer.MapContext(saltedSeed, context, (uint)policyDecision.Value.Length);
            MultiActionHelper.PutActionToList(decision.Value, policyDecision.Value);

            return Decision.Create(policyDecision.Value, decision.ExplorerState, policyDecision, decision.ShouldRecord);
        }
    }
}