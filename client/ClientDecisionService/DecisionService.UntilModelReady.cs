using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.ComponentModel;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DecisionService_UntilModelReady
    {
        public static DecisionServiceClient<TContext>
            ExploitUntilModelReady<TContext>
            (this DecisionServiceClient<TContext> that, IContextMapper<TContext, ActionProbability[]> initialPolicy)
        {
            that.InitialPolicy = initialPolicy;
            return that;
        }
    }
}
