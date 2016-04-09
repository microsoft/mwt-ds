using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class ExplorerFactory
    {
        /*
        public static TopSlotExplorer<TContext, TExplorer>
            CreateTopSlot<TContext, TExplorer>(
                IContextMapper<TContext, int[]> defaultRanker,
                Func<IContextMapper<TContext, int>, TExplorer> singleExplorerFactory,
                int numActions)
                where TExplorer : IVariableActionExplorer<TContext, int, int>
        {
            return new TopSlotExplorer<TContext, TExplorer>(defaultRanker, singleExplorerFactory, numActions);
        }
        */
        // TODO: Add back
        //internal static TopSlotExplorer<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>
        //    CreateTopSlotEpsilonGreedyExplorer<TContext>(
        //        IContextMapper<TContext, int[]> defaultPolicy,
        //        float epsilon,
        //        int numActionsVariable = int.MaxValue)
        //{
        //    return Create<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>(
        //        defaultPolicy,
        //        policy => new EpsilonGreedyExplorer<TContext>(policy, epsilon, numActionsVariable),
        //        numActionsVariable);
        //}

        //internal static TopSlotExplorer<TContext, TauFirstExplorer<TContext>, TauFirstState>
        //    CreateTopSlotTauFirstExplorer<TContext>(
        //        IContextMapper<TContext, int[]> defaultPolicy,
        //        int tau,
        //        int numActionsVariable = int.MaxValue)
        //{
        //    return Explorer<TContext>.CreateTopSlotTauFirstExplorer(defaultPolicy, tau, numActionsVariable);
        //}

        //internal static EpsilonGreedyExplorer<TContext> CreateEpsilonGreedyExplorer<TContext>(IContextMapper<TContext, int> defaultPolicy, float epsilon, int numActionsVariable = int.MaxValue)
        //{
        //    return new EpsilonGreedyExplorer<TContext>(defaultPolicy, epsilon, numActionsVariable);
        //}

        //internal static TauFirstExplorer<TContext> CreateTauFirstExplorer<TContext>(IContextMapper<TContext, int> defaultPolicy, int tau, int numActionsVariable = int.MaxValue)
        //{
        //    return new TauFirstExplorer<TContext>(defaultPolicy, tau, numActionsVariable);
        //}

        //internal static SoftmaxExplorer<TContext> CreateSoftmaxExplorer<TContext>(IContextMapper<TContext, float[]> defaultScorer, float lambda, int numActionsVariable = int.MaxValue)
        //{
        //    return new SoftmaxExplorer<TContext>(defaultScorer, lambda, numActionsVariable);
        //}

        // TODO: add more factory methods
    }
}
