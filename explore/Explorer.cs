using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class Explorer<TContext>
    {
        //public static TopSlotExplorer<TContext, TauFirstExplorer<TContext>, TauFirstState>
        //    CreateTopSlotTauFirstExplorer(
        //        IContextMapper<TContext, int[]> defaultPolicy,
        //        int tau,
        //        int numActionsVariable = int.MaxValue)
        //{
        //    return Create<TContext, TauFirstExplorer<TContext>, TauFirstState>(
        //        defaultPolicy,
        //        policy => new TauFirstExplorer<TContext>(policy, tau, numActionsVariable),
        //        numActionsVariable);
        //}

        //internal static TopSlotExplorer<TContext, TExplorer, TExplorerState> Create<TExplorer, TExplorerState>(
        //    IContextMapper<TContext, int[]> defaultRanker,
        //    Func<IContextMapper<TContext, int>, TExplorer> singleExplorerFactory,
        //    int numActions)
        //where TExplorer : 
        //    IExplorer<TContext, int, TExplorerState, int>
        //{
        //    return new TopSlotExplorer<TContext, TExplorer, TExplorerState>(defaultRanker, singleExplorerFactory, numActions);
        //}

        //internal static TopSlotExplorer<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>
        //    CreateTopSlotEpsilonGreedyExplorer(
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
        //    CreateTopSlotTauFirstExplorer(
        //        IContextMapper<TContext, int[]> defaultPolicy, 
        //        int tau, 
        //        int numActionsVariable = int.MaxValue)
        //{
        //    return Explorer<TContext>.CreateTopSlotTauFirstExplorer(defaultPolicy, tau, numActionsVariable);
        //}

        public static EpsilonGreedyExplorer<TContext> CreateEpsilonGreedyExplorer(IContextMapper<TContext, int> defaultPolicy, float epsilon, int numActionsVariable = int.MaxValue)
        {
            return new EpsilonGreedyExplorer<TContext>(defaultPolicy, epsilon, numActionsVariable);
        }

        //internal static TauFirstExplorer<TContext> CreateTauFirstExplorer(IContextMapper<TContext, int> defaultPolicy, int tau, int numActionsVariable = int.MaxValue)
        //{
        //    return new TauFirstExplorer<TContext>(defaultPolicy, tau, numActionsVariable);
        //}

        //internal static SoftmaxExplorer<TContext> CreateSoftmaxExplorer(IContextMapper<TContext, float[]> defaultScorer, float lambda, int numActionsVariable = int.MaxValue)
        //{
        //    return new SoftmaxExplorer<TContext>(defaultScorer, lambda, numActionsVariable);
        //}

        // TODO: add more factory methods
    }
}
