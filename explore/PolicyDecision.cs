using Newtonsoft.Json;
namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    /// <summary>
    /// Decision result from a policy. 
    /// </summary>
    [JsonConverter(PolicyDecisionJsonConverter)]
    public class PolicyDecision<TAction, TPolicyState>
    {
        // int, int[]
        // choose action (shown)
        public TAction Action { get; set; }

        public TPolicyState PolicyState { get; set; }
    }
}

/*
namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.Core
{
    /// <summary>
    /// Decision result from a policy. 
    /// </summary>
    public class BasePolicyDecisionTuple
    {
        /// <summary>
        /// The Id of the model used to make predictions/decisions, if any exists at decision time.
        /// </summary>
        public string ModelId { get; set; }
    }
}

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction
{
    using Microsoft.Research.MultiWorldTesting.ExploreLibrary.Core;

    /// <summary>
    /// Decision result from a policy.
    /// </summary>
    public class PolicyDecisionTuple : BasePolicyDecisionTuple
    {
        /// <summary>
        /// Action chosen by exploration.
        /// </summary>
        public uint Action { get; set; }
    }
}

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction
{
    using Microsoft.Research.MultiWorldTesting.ExploreLibrary.Core;

    /// <summary>
    /// Decision result from a policy. 
    /// </summary>
    public class PolicyDecisionTuple : BasePolicyDecisionTuple
    {
        /// <summary>
        /// List of actions chosen by exploration.
        /// </summary>
        public uint[] Actions { get; set; }
    }
}
*/