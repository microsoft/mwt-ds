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
        public static Decision<TValue, TExplorerState, TMapperValue, TMapperState> Create<TValue, TExplorerState, TMapperValue, TMapperState>(
            TValue action, TExplorerState explorerState, Decision<TMapperValue, TMapperState> policyDecision, bool shouldRecord)
        {
            return new Decision<TValue, TExplorerState, TMapperValue, TMapperState>
            {
                Value = action,
                ExplorerState = explorerState,
                MapperDecision = policyDecision,
                ShouldRecord = shouldRecord
            };
        }

        public static Decision<TValue, TMapperState> Create<TValue, TMapperState>(TValue action, TMapperState policyState)
        {
            return new Decision<TValue, TMapperState>
            {
                Value = action,
                MapperState = policyState
            };
        }
    }

    [JsonConverter(typeof(DecisionJsonConverter))]
    public sealed class Decision<TValue, TExplorerState, TMapperValue, TMapperState>
    {
        public bool ShouldRecord { get; set; }

        // int, int[]
        // choose action (shown)
        [JsonProperty("a")]
        public TValue Value { get; set; }

        // probability | predicted ranking, epsilon
        // "EpsilonGreedyLog":{ ... } 
        public TExplorerState ExplorerState { get; set; }

        // only logging TMapperState
        public Decision<TMapperValue, TMapperState> MapperDecision { get; set; }
    }

    /// <summary>
    /// Decision result from a policy. 
    /// </summary>
    [JsonConverter(typeof(PolicyDecisionJsonConverter))]
    public sealed class Decision<TValue, TState>
    {
        // int, int[]
        // choose action (shown)
        public TValue Value { get; set; }

        public TState MapperState { get; set; }
    }
}
