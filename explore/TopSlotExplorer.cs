using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public class TopSlotExplorer<TContext, TExplorer, TExplorerState> : BaseExplorer<TContext, int[], TExplorerState, int[]>
        where TExplorer : IVariableActionExplorer<TContext, int, TExplorerState, int>
    {
        private class TopSlotPolicy : IPolicy<TContext>
        {
            private readonly IContextMapper<TContext, int[]> policy;

            // TODO: review if we can remove this ThreadLocal
            private ThreadLocal<int?> action = new ThreadLocal<int?>();

            internal TopSlotPolicy(IContextMapper<TContext, int[]> policy)
            {
                this.policy = policy;
            }

            public void UpdateAction(int? action)
            {
                this.action.Value = action;
            }

            public Decision<int> MapContext(TContext context)
            {
                return this.action.Value ?? null;
            }
        }

        private readonly TExplorer singleExplorer;
        private readonly TopSlotPolicy topSlotPolicy;
        private readonly INumberOfActionsProvider<TContext> numberOfActionsProvider;

        public TopSlotExplorer(IContextMapper<TContext, int[]> defaultPolicy,
            Func<IPolicy<TContext>, TExplorer> singleExplorerFactory, 
            int numActions = int.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.topSlotPolicy = new TopSlotPolicy(defaultPolicy);
            this.singleExplorer = singleExplorerFactory(this.topSlotPolicy);
            this.numberOfActionsProvider = defaultPolicy as INumberOfActionsProvider<TContext>;
        }

        public override Decision<int[], TExplorerState, int[]> MapContext(ulong saltedSeed, TContext context)
        {
            var policyDecision = this.contextMapper.MapContext(context);

            int? topAction;
            int numActions;
            if (policyDecision == null)
            {
                // handle policy that's not loaded yet
                if (this.numberOfActionsProvider == null)
                    throw new InvalidOperationException(string.Format("Policy '{0}' is unable to provide decision AND does not implement INumberOfActionsProvider", this.contextMapper.GetType()));

                numActions = this.numberOfActionsProvider.GetNumberOfActions(context);
                topAction = null;
                policyDecision = Decision.Create(
                    Enumerable.Range(1, numActions).ToArray());
            }
            else
            {
                if (policyDecision.Value == null || policyDecision.Value.Length < 1)
                {
                    throw new ArgumentException("Actions chosen by default policy must not be empty.");
                }

                numActions = policyDecision.Value.Length;
                topAction = policyDecision.Value[0];
            }

            this.topSlotPolicy.UpdateAction(topAction);
            var decision = this.singleExplorer.MapContext(saltedSeed, context, numActions);
            MultiActionHelper.PutActionToList(decision.Value, policyDecision.Value);

            return Decision.Create(policyDecision.Value, decision.ExplorerState, policyDecision, decision.ShouldRecord);
        }
    }
}