using MultiWorldTesting;
using System;

namespace DecisionSample
{
    /* Temp classes to support interface */

    public interface IExploreAlgorithm<TContext>
    {
        IExplorer<TContext> Get();
    }
    public class EpsilonGreedyExplorer<TContext> : IExploreAlgorithm<TContext>
    {
        public EpsilonGreedyExplorer(IPolicy<TContext> policy, float epsilon, uint numActions)
        {
            Epsilon = epsilon;
            NumActions = numActions;
            Policy = policy;
        }

        public IPolicy<TContext> Policy { get; set; }
        public float Epsilon { get; set; }
        public uint NumActions { get; set; }

        public IExplorer<TContext> Get()
        {
            return new MultiWorldTesting.EpsilonGreedyExplorer<TContext>(Policy, Epsilon, NumActions);
        }
    }

    public class TauFirstExplorer<TContext> : IExploreAlgorithm<TContext>
    {
        public TauFirstExplorer(IPolicy<TContext> policy, uint tau, uint numActions)
        {
            Tau = tau;
            NumActions = numActions;
            Policy = policy;
        }

        public IPolicy<TContext> Policy { get; set; }
        public uint Tau { get; set; }
        public uint NumActions { get; set; }

        public IExplorer<TContext> Get()
        {
            return new MultiWorldTesting.TauFirstExplorer<TContext>(Policy, Tau, NumActions);
        }
    }

    public class BootstrapExplorer<TContext> : IExploreAlgorithm<TContext>
    {
        public BootstrapExplorer(IPolicy<TContext>[] policies, uint numActions)
        {
            NumActions = numActions;
            Policies = policies;
        }

        public IPolicy<TContext>[] Policies { get; set; }
        public uint NumActions { get; set; }

        public IExplorer<TContext> Get()
        {
            return new MultiWorldTesting.BootstrapExplorer<TContext>(Policies, NumActions);
        }
    }

    public class SoftmaxExplorer<TContext> : IExploreAlgorithm<TContext>
    {
        public SoftmaxExplorer(IScorer<TContext> scorer, float lambda, uint numActions)
        {
            Lambda = lambda;
            NumActions = numActions;
            Scorer = scorer;
        }

        public IScorer<TContext> Scorer { get; set; }
        public float Lambda { get; set; }
        public uint NumActions { get; set; }

        public IExplorer<TContext> Get()
        {
            return new MultiWorldTesting.SoftmaxExplorer<TContext>(Scorer, Lambda, NumActions);
        }
    }

    public class GenericExplorer<TContext> : IExploreAlgorithm<TContext>
    {
        public GenericExplorer(IScorer<TContext> scorer, uint numActions)
        {
            NumActions = numActions;
            Scorer = scorer;
        }

        public IScorer<TContext> Scorer { get; set; }
        public uint NumActions { get; set; }

        public IExplorer<TContext> Get()
        {
            return new MultiWorldTesting.GenericExplorer<TContext>(Scorer, NumActions);
        }
    }
}
