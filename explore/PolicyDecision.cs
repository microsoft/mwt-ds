using Newtonsoft.Json;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class PolicyDecision
    {
        public static PolicyDecision<TAction, TPolicyState> Create<TAction, TPolicyState>(TAction action, TPolicyState policyState)
        {
            return new PolicyDecision<TAction, TPolicyState>
            {
                Action = action,
                PolicyState = policyState
            };
        }
    }

    /// <summary>
    /// Decision result from a policy. 
    /// </summary>
    [JsonConverter(typeof(PolicyDecisionJsonConverter))]
    public class PolicyDecision<TAction, TPolicyState>
    {
        // int, int[]
        // choose action (shown)
        public TAction Action { get; set; }

        public TPolicyState PolicyState { get; set; }
    }
}