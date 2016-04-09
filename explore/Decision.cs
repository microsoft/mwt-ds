using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public static class ExplorerDecision
    {
        public static ExplorerDecision<TValue> Create<TValue>(TValue action, object explorerState, bool shouldRecord)
        {
            return new ExplorerDecision<TValue>
            {
                Value = action,
                ExplorerState = explorerState,
                ShouldRecord = shouldRecord
            };
        }
    }

    public sealed class ExplorerDecision<TValue>
    {
        public bool ShouldRecord { get; set; }

        // int, int[]
        // choose action (shown)
        
        public TValue Value { get; set; }

        // probability | predicted ranking, epsilon
        // "EpsilonGreedyLog":{ ... } 
        public object ExplorerState { get; set; }
    }
/*
        // only logging TMapperState
        public Decision<TMapperValue> MapperDecision { get; set; }
    }
*/

    public static class PolicyDecision
    {
        public static PolicyDecision<TValue> Create<TValue>(TValue action, object policyState = null)
        {
            return new PolicyDecision<TValue>
            {
                Value = action,
                MapperState = policyState
            };
        }
    }

    /// <summary>
    /// Decision result from a policy. 
    /// </summary>
    public class PolicyDecision<TValue>
    {
        // int, int[], float[]
        // choose action (shown)
        public TValue Value { get; set; }

        public object MapperState { get; set; }

        static public implicit operator PolicyDecision<TValue>(TValue value)
        {
            return new PolicyDecision<TValue> { Value = value };
        }
    }
}
