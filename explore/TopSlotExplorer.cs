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

            // TODO: review if we can remove this ThreadLocal
            private ThreadLocal<uint?> action = new ThreadLocal<uint?>();

            internal TopSlotPolicy(IContextMapper<TContext, uint[]> policy)
            {
                this.policy = policy;
            }

            public void UpdateAction(uint? action)
            {
                this.action.Value = action;
            }

            public Decision<uint> MapContext(TContext context)
            {
                return this.action.Value ?? null;
            }
        }

        private readonly TExplorer singleExplorer;
        private readonly TopSlotPolicy topSlotPolicy;
        private readonly INumberOfActionsProvider<TContext> numberOfActionsProvider;

        public TopSlotExplorer(IContextMapper<TContext, uint[]> defaultPolicy,
            Func<IPolicy<TContext>, TExplorer> singleExplorerFactory, 
            uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.topSlotPolicy = new TopSlotPolicy(defaultPolicy);
            this.singleExplorer = singleExplorerFactory(this.topSlotPolicy);
            this.numberOfActionsProvider = defaultPolicy as INumberOfActionsProvider<TContext>;
        }

        public override Decision<uint[], TExplorerState, uint[]> MapContext(ulong saltedSeed, TContext context)
        {
            var policyDecision = this.contextMapper.MapContext(context);

            uint? topAction;
            uint numActions;
            if (policyDecision == null)
            {
                // handle policy that's not loaded yet
                if (this.numberOfActionsProvider == null)
                    throw new InvalidOperationException(string.Format("Policy '{0}' is unable to provide decision AND does not implement INumberOfActionsProvider", this.contextMapper.GetType()));

                numActions = (uint)this.numberOfActionsProvider.GetNumberOfActions(context);
                topAction = null;
                policyDecision = Decision.Create(
                    Enumerable.Range(1, (int)numActions).Select(a => (uint)a).ToArray());
            }
            else
            {
                if (policyDecision.Value == null || policyDecision.Value.Length < 1)
                {
                    throw new ArgumentException("Actions chosen by default policy must not be empty.");
                }

                numActions = (uint)policyDecision.Value.Length;
                topAction = policyDecision.Value[0];
            }

            this.topSlotPolicy.UpdateAction(topAction);
            var decision = this.singleExplorer.MapContext(saltedSeed, context, numActions);
            MultiActionHelper.PutActionToList(decision.Value, policyDecision.Value);

            return Decision.Create(policyDecision.Value, decision.ExplorerState, policyDecision, decision.ShouldRecord);
        }
    }
}