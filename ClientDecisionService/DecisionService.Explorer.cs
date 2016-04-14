using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.ComponentModel;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DecisionService_Explorer
    {
        public static DecisionServiceClient<TContext, int, int> 
            WithEpsilonGreedy<TContext>
            (this DecisionServiceClient<TContext, int, int> that, float epsilon)
        {
            that.Explorer = new EpsilonGreedyExplorer(epsilon);
            return that;
        }

        public static DecisionServiceClient<TContext, int, int>
            WithTauFirst<TContext>
            (this DecisionServiceClient<TContext, int, int> that, int tau)
        {
            that.Explorer = new TauFirstExplorer(tau);
            return that;
        }

        public static DecisionServiceClient<TContext, int[], int[]> 
            WithTopSlotEpsilonGreedy<TContext>
            (this DecisionServiceClient<TContext, int[], int[]> that, float epsilon)
        {
            that.Explorer = new TopSlotExplorer(new EpsilonGreedyExplorer(epsilon));
            return that;
        }

        public static DecisionServiceClient<TContext, int[], int[]>
            WithEpsilonGreedySlate<TContext>
            (this DecisionServiceClient<TContext, int[], int[]> that, float epsilon)
        {
            that.Explorer = new EpsilonGreedySlateExplorer(epsilon);
            return that;
        }
    }
}
