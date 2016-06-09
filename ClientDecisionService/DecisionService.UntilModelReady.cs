using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.ComponentModel;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //public static class DecisionService_UntilModelReady
    //{
    //    public static DecisionServiceClient<TContext, TAction, TPolicyValue>
    //        ExploitUntilModelReady<TContext, TAction, TPolicyValue>
    //        (this DecisionServiceClient<TContext, TAction, TPolicyValue> that, IContextMapper<TContext, TPolicyValue> initialPolicy)
    //    {
    //        that.InitialPolicy = initialPolicy;
    //        return that;
    //    }

    //    // Policy exploration strategies

    //    public static DecisionServiceClient<TContext, int, TPolicyValue>
    //        ExploreUniformRandomUntilModelReady<TContext, TPolicyValue>
    //        (this DecisionServiceClient<TContext, int, TPolicyValue> that)
    //    {
    //        that.InitialExplorer = new UniformRandomExploration();
    //        return that;
    //    }

    //    // Ranking exploration strategies

    //    public static DecisionServiceClient<TContext, int[], TPolicyValue>
    //        ExploreUniformPermutationsUntilModelReady<TContext, TPolicyValue>
    //        (this DecisionServiceClient<TContext, int[], TPolicyValue> that)
    //    {
    //        that.InitialExplorer = new PermutationExplorer();
    //        return that;
    //    }

    //    public static DecisionServiceClient<TContext, int[], TPolicyValue>
    //        ExploreTopSlotUniformRandomUntilModelReady<TContext, TPolicyValue>
    //        (this DecisionServiceClient<TContext, int[], TPolicyValue> that)
    //    {
    //        that.InitialExplorer = new PermutationExplorer(1);
    //        return that;
    //    }
    //}
}
