using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class ExplorerFactory
    {
        public static TopSlotExplorer<TContext, TExplorer, TExplorerState>
            CreateTopSlot<TContext, TExplorer, TExplorerState>(
                IContextMapper<TContext, uint[]> defaultRanker,
                Func<IContextMapper<TContext, uint>, TExplorer> singleExplorerFactory,
                uint numActions)
                where TExplorer : IVariableActionExplorer<TContext, uint, TExplorerState, uint>
        {
            return new TopSlotExplorer<TContext, TExplorer, TExplorerState>(defaultRanker, singleExplorerFactory, numActions);
        }

        // TODO: Add back
        //internal static TopSlotExplorer<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>
        //    CreateTopSlotEpsilonGreedyExplorer<TContext>(
        //        IContextMapper<TContext, uint[]> defaultPolicy,
        //        float epsilon,
        //        uint numActionsVariable = uint.MaxValue)
        //{
        //    return Create<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>(
        //        defaultPolicy,
        //        policy => new EpsilonGreedyExplorer<TContext>(policy, epsilon, numActionsVariable),
        //        numActionsVariable);
        //}

        //internal static TopSlotExplorer<TContext, TauFirstExplorer<TContext>, TauFirstState>
        //    CreateTopSlotTauFirstExplorer<TContext>(
        //        IContextMapper<TContext, uint[]> defaultPolicy,
        //        uint tau,
        //        uint numActionsVariable = uint.MaxValue)
        //{
        //    return Explorer<TContext>.CreateTopSlotTauFirstExplorer(defaultPolicy, tau, numActionsVariable);
        //}

        //internal static EpsilonGreedyExplorer<TContext> CreateEpsilonGreedyExplorer<TContext>(IContextMapper<TContext, uint> defaultPolicy, float epsilon, uint numActionsVariable = uint.MaxValue)
        //{
        //    return new EpsilonGreedyExplorer<TContext>(defaultPolicy, epsilon, numActionsVariable);
        //}

        //internal static TauFirstExplorer<TContext> CreateTauFirstExplorer<TContext>(IContextMapper<TContext, uint> defaultPolicy, uint tau, uint numActionsVariable = uint.MaxValue)
        //{
        //    return new TauFirstExplorer<TContext>(defaultPolicy, tau, numActionsVariable);
        //}

        //internal static SoftmaxExplorer<TContext> CreateSoftmaxExplorer<TContext>(IContextMapper<TContext, float[]> defaultScorer, float lambda, uint numActionsVariable = uint.MaxValue)
        //{
        //    return new SoftmaxExplorer<TContext>(defaultScorer, lambda, numActionsVariable);
        //}

        // TODO: add more factory methods
    }
}
