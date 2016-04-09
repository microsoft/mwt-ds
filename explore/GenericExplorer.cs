using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    [JsonObject(Id = "stgr")]
    public class GenericExplorerState
    {
        [JsonProperty(PropertyName = "p")]
        public float Probability { get; set; }
    }

    /// <summary>
	/// The generic exploration class.
	/// </summary>
	/// <remarks>
	/// GenericExplorer provides complete flexibility.  You can create any
	/// distribution over actions desired, and it will draw from that.
	/// </remarks>
	/// <typeparam name="TContext">The Context type.</typeparam>
    public class GenericExplorer : BaseExplorer<int, float[]>
	{
		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultScorer">A function which outputs the probability of each action.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public GenericExplorer(int numActions = int.MaxValue)
            : base(numActions)
		{
        }

        public override ExplorerDecision<int> MapContext(ulong saltedSeed, float[] weights)
        {
            int numWeights = weights.Length;
            if (this.numActionsFixed != int.MaxValue && numWeights != this.numActionsFixed)
                throw new ArgumentException("The number of weights returned by the scorer must equal number of actions");

            // Create a discrete_distribution based on the returned weights. This class handles the
            // case where the sum of the weights is < or > 1, by normalizing agains the sum.
            float total = 0f;
            for (int i = 0; i < numWeights; i++)
            {
                if (weights[i] < 0)
                    throw new ArgumentException("Scores must be non-negative.");

                total += weights[i];
            }

            if (total == 0)
                throw new ArgumentException("At least one score must be positive.");

            var random = new PRG(saltedSeed);
            float draw = random.UniformUnitInterval();

            float sum = 0f;
            float actionProbability = 0f;
            int actionIndex = numWeights - 1;
            for (int i = 0; i < numWeights; i++)
            {
                weights[i] = weights[i] / total;
                sum += weights[i];
                if (sum > draw)
                {
                    actionIndex = i;
                    actionProbability = weights[i];
                    break;
                }
            }

            actionIndex++;

            // action id is one-based
            return ExplorerDecision.Create(
                actionIndex,
                new GenericExplorerState { Probability = actionProbability },
                true);
        }
    }

    /// <summary>
    /// The generic exploration class.
    /// </summary>
    /// <remarks>
    /// GenericExplorer provides complete flexibility.  You can create any
    /// distribution over actions desired, and it will draw from that.
    /// </remarks>
    /// <typeparam name="TContext">The Context type.</typeparam>
    public sealed class GenericExplorerSampleWithoutReplacement 
        : BaseExplorer<int[], float[]>
    {
        private readonly GenericExplorer explorer;

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultScorer">A function which outputs the probability of each action.</param>
        /// <param name="numActions">The number of actions to randomize over.</param>
        public GenericExplorerSampleWithoutReplacement(int numActions = int.MaxValue)
             : base(numActions)
        {
            this.explorer = new GenericExplorer(numActions);
        }

        public override void EnableExplore(bool explore)
        {
            base.EnableExplore(explore);
            this.explorer.EnableExplore(explore);
        }

        public override ExplorerDecision<int[]> MapContext(ulong saltedSeed, float[] weights)
        {
            var random = new PRG(saltedSeed);

            var decision = this.explorer.MapContext(saltedSeed, weights);

            float actionProbability = 0f;
            int[] chosenActions = MultiActionHelper.SampleWithoutReplacement(weights, weights.Length, random, ref actionProbability);

            // action id is one-based
            return ExplorerDecision.Create(chosenActions,
                new GenericExplorerState { Probability = actionProbability },
                true);
        }
    }
}
