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
        public static Decision<TValue, TExplorerState, TMapperValue> Create<TValue, TExplorerState, TMapperValue>(
            TValue action, TExplorerState explorerState, Decision<TMapperValue> policyDecision, bool shouldRecord)
        {
            return new Decision<TValue, TExplorerState, TMapperValue>
            {
                Value = action,
                ExplorerState = explorerState,
                MapperDecision = policyDecision,
                ShouldRecord = shouldRecord
            };
        }

        public static Decision<TValue> Create<TValue>(TValue action, object policyState = null)
        {
            return new Decision<TValue>
            {
                Value = action,
                MapperState = policyState
            };
        }
    }

    public sealed class Decision<TValue, TExplorerState, TMapperValue>
    {
        public bool ShouldRecord { get; set; }

        // int, int[]
        // choose action (shown)
        
        public TValue Value { get; set; }

        // probability | predicted ranking, epsilon
        // "EpsilonGreedyLog":{ ... } 
        public TExplorerState ExplorerState { get; set; }

        // only logging TMapperState
        public Decision<TMapperValue> MapperDecision { get; set; }
    }

    /// <summary>
    /// Decision result from a policy. 
    /// </summary>
    public class Decision<TValue>
    {
        // int, int[], float[]
        // choose action (shown)
        public TValue Value { get; set; }

        public object MapperState { get; set; }

        static public implicit operator Decision<TValue>(TValue value)
        {
            return new Decision<TValue> { Value = value };
        }
    }
}
