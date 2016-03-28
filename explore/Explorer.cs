using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class Explorer
    {
        private static TopSlotExplorer<TContext, TExplorer, TExplorerState, TMapperState> Create<TContext, TExplorer, TExplorerState, TMapperState>(
            IRanker<TContext, TMapperState> defaultPolicy,
            Func<IPolicy<TContext, Decision<uint[], TMapperState>>, TExplorer> singleExplorerFactory,
            uint numActions)
        where TExplorer : 
            IExplorer<TContext, uint, TExplorerState, uint, Decision<uint[], TMapperState>>,
                IConsumeContextMapper<TContext, uint, Decision<uint[], TMapperState>>
        {
            return new TopSlotExplorer<TContext, TExplorer, TExplorerState, TMapperState>(defaultPolicy, singleExplorerFactory, numActions);
        }

        public static TopSlotExplorer<TContext, EpsilonGreedyExplorer<TContext, Decision<uint[], TMapperState>>, EpsilonGreedyState, TMapperState>
            CreateTopSlotEpsilonGreedyExplorer<TContext, TMapperState>(
                IRanker<TContext, TMapperState> defaultPolicy, 
                float epsilon,
                uint numActionsVariable = uint.MaxValue)
        {
            return Create<TContext, EpsilonGreedyExplorer<TContext, Decision<uint[], TMapperState>>, EpsilonGreedyState, TMapperState>(
                defaultPolicy,
                policy => new EpsilonGreedyExplorer<TContext, Decision<uint[], TMapperState>>(policy, epsilon, numActionsVariable),
                numActionsVariable);
        }

        public static TopSlotExplorer<TContext, TauFirstExplorer<TContext, Decision<uint[], TMapperState>>, TauFirstState, TMapperState>
            CreateTopSlotTauFirstExplorer<TContext, TMapperState>(
                IRanker<TContext, TMapperState> defaultPolicy, 
                uint tau, 
                uint numActionsVariable = uint.MaxValue)
        {
            return Create<TContext, TauFirstExplorer<TContext, Decision<uint[], TMapperState>>, TauFirstState, TMapperState>(
                defaultPolicy,
                policy => new TauFirstExplorer<TContext, Decision<uint[], TMapperState>>(policy, tau, numActionsVariable),
                numActionsVariable);
        }

        public static EpsilonGreedyExplorer<TContext, TMapperState> CreateEpsilonGreedyExplorer<TContext, TMapperState>(IPolicy<TContext, TMapperState> defaultPolicy, float epsilon, uint numActionsVariable = uint.MaxValue)
        {
            return new EpsilonGreedyExplorer<TContext, TMapperState>(defaultPolicy, epsilon, numActionsVariable);
        }

        public static TauFirstExplorer<TContext, TMapperState> CreateTauFirstExplorer<TContext, TMapperState>(IPolicy<TContext, TMapperState> defaultPolicy, uint tau, uint numActionsVariable = uint.MaxValue)
        {
            return new TauFirstExplorer<TContext, TMapperState>(defaultPolicy, tau, numActionsVariable);
        }

        public static SoftmaxExplorer<TContext, TMapperState> CreateSoftmaxExplorer<TContext, TMapperState>(IScorer<TContext, TMapperState> defaultScorer, float lambda, uint numActionsVariable = uint.MaxValue)
        {
            return new SoftmaxExplorer<TContext, TMapperState>(defaultScorer, lambda, numActionsVariable);
        }

        // TODO: add more factory methods
    }
}
