using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class Decision
    {
        public static Decision<TAction, TExplorerState, TPolicyState> Create(TAction action, TExplorerState explorerState, PolicyDecision<TAction, TPolicyState> policyDecision)
        {
            return new Decision<TAction, TExplorerState, TPolicyState>
            {
                Action = action,
                ExplorerState = explorerState,
                PolicyDecision = policyDecision
            };
        }
    }

    [JsonConverter(typeof(DecisionJsonConverter))]
    public sealed class Decision<TAction, TExplorerState, TPolicyState>
    {
        // int, int[]
        // choose action (shown)
        [JsonProperty(PropertyName: "a")]
        public TAction Action { get; set; }

        // probability | predicted ranking, epsilon
        // "EpsilonGreedyLog":{ ... } 
        public TExplorerState ExplorerState { get; set; }

        // only logging TPolicyState
        public PolicyDecision<TAction, TPolicyState> PolicyDecision { get; set; }
    }
}
