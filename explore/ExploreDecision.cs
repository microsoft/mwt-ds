
namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.Core
{
    /*
    public class EpsilonGreedyLog
    {
        public float Probability { get; set; }
    }

    public class EpsilonGreedySlateLog
    {
        public float Epsilon { get; set; }

        public int[] PredictedActions { get; set; }
    }

    public class VowpalWabbitLog
    {
        public string ModelID { get; set; }
    }
     * */

    /// <summary>
    /// Exploration result 
    /// </summary>
    public class BaseDecisionTuple
    {
        /// <summary>
        /// Probability of choosing the action.
        /// </summary>
        public float Probability { get; set; }

        /// <summary>
        /// Whether to record/log the exploration result. 
        /// </summary>
        public bool ShouldRecord { get; set; }

        /// <summary>
        /// The Id of the model used to make predictions/decisions, if any exists at decision time.
        /// </summary>
        public string ModelId { get; set; }

        /// <summary>
        /// Indicates whether the decision was generated purely from exploration (vs. exploitation).
        /// This value is only relevant to Epsilon Greedy or Tau First algorithms.
        /// </summary>
        public bool? IsExplore { get; set; }
    }
}

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction
{
    using Microsoft.Research.MultiWorldTesting.ExploreLibrary.Core;

    /// <summary>
    /// Exploration result 
    /// </summary>
    public class DecisionTuple : BaseDecisionTuple
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
    /// Exploration result 
    /// </summary>
    public class DecisionTuple : BaseDecisionTuple
    {
        /// <summary>
        /// List of actions chosen by exploration.
        /// </summary>
        public uint[] Actions { get; set; }
    }
}
