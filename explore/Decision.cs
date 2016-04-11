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
        public static ExplorerDecision<TAction> Create<TAction>(TAction action, object explorerState, bool shouldRecord)
        {
            return new ExplorerDecision<TAction>
            {
                Value = action,
                ExplorerState = explorerState,
                ShouldRecord = shouldRecord
            };
        }
    }

    public sealed class ExplorerDecision<TAction>
    {
        public bool ShouldRecord { get; set; }

        // int, int[]
        // choose action (shown)
        
        public TAction Value { get; set; }

        // probability | predicted ranking, epsilon
        // "EpsilonGreedyLog":{ ... } 
        public object ExplorerState { get; set; }
    }
/*
        // only logging TMapperState
        public Decision<TPolicyValue> MapperDecision { get; set; }
    }
*/

    public static class PolicyDecision
    {
        public static PolicyDecision<TAction> Create<TAction>(TAction action, object policyState = null)
        {
            return new PolicyDecision<TAction>
            {
                Value = action,
                MapperState = policyState
            };
        }
    }

    /// <summary>
    /// Decision result from a policy. 
    /// </summary>
    public class PolicyDecision<TAction>
    {
        // int, int[], float[]
        // choose action (shown)
        public TAction Value { get; set; }

        public object MapperState { get; set; }

        static public implicit operator PolicyDecision<TAction>(TAction value)
        {
            return new PolicyDecision<TAction> { Value = value };
        }
    }
}
