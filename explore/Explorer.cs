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
        //        IContextMapper<TContext, uint[]> defaultPolicy,
        //        uint tau,
        //        uint numActionsVariable = uint.MaxValue)
        //{
        //    return Create<TContext, TauFirstExplorer<TContext>, TauFirstState>(
        //        defaultPolicy,
        //        policy => new TauFirstExplorer<TContext>(policy, tau, numActionsVariable),
        //        numActionsVariable);
        //}

        //internal static TopSlotExplorer<TContext, TExplorer, TExplorerState> Create<TExplorer, TExplorerState>(
        //    IContextMapper<TContext, uint[]> defaultRanker,
        //    Func<IContextMapper<TContext, uint>, TExplorer> singleExplorerFactory,
        //    uint numActions)
        //where TExplorer : 
        //    IExplorer<TContext, uint, TExplorerState, uint>
        //{
        //    return new TopSlotExplorer<TContext, TExplorer, TExplorerState>(defaultRanker, singleExplorerFactory, numActions);
        //}

        //internal static TopSlotExplorer<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>
        //    CreateTopSlotEpsilonGreedyExplorer(
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
        //    CreateTopSlotTauFirstExplorer(
        //        IContextMapper<TContext, uint[]> defaultPolicy, 
        //        uint tau, 
        //        uint numActionsVariable = uint.MaxValue)
        //{
        //    return Explorer<TContext>.CreateTopSlotTauFirstExplorer(defaultPolicy, tau, numActionsVariable);
        //}

        public static EpsilonGreedyExplorer<TContext> CreateEpsilonGreedyExplorer(IContextMapper<TContext, uint> defaultPolicy, float epsilon, uint numActionsVariable = uint.MaxValue)
        {
            return new EpsilonGreedyExplorer<TContext>(defaultPolicy, epsilon, numActionsVariable);
        }

        //internal static TauFirstExplorer<TContext> CreateTauFirstExplorer(IContextMapper<TContext, uint> defaultPolicy, uint tau, uint numActionsVariable = uint.MaxValue)
        //{
        //    return new TauFirstExplorer<TContext>(defaultPolicy, tau, numActionsVariable);
        //}

        //internal static SoftmaxExplorer<TContext> CreateSoftmaxExplorer(IContextMapper<TContext, float[]> defaultScorer, float lambda, uint numActionsVariable = uint.MaxValue)
        //{
        //    return new SoftmaxExplorer<TContext>(defaultScorer, lambda, numActionsVariable);
        //}

        // TODO: add more factory methods
    }
}
