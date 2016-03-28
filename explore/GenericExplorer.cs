using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
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
    public class GenericExplorer<TContext, TMapperState> : BaseExplorer<TContext, uint, GenericExplorerState, float[], TMapperState>
	{
		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultScorer">A function which outputs the probability of each action.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public GenericExplorer(IContextMapper<TContext, float[], TMapperState> defaultScorer, uint numActions = uint.MaxValue)
            : base(defaultScorer, numActions)
		{
        }

        protected override Decision<uint, GenericExplorerState, float[], TMapperState> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            var random = new PRG(saltedSeed);

            // Invoke the default scorer function
            Decision<float[], TMapperState> policyDecision = this.defaultPolicy.MapContext(context);
            float[] weights = policyDecision.Value;

            uint numWeights = (uint)weights.Length;
            if (numWeights != numActionsVariable)
            {
                throw new ArgumentException("The number of weights returned by the scorer must equal number of actions");
            }

            // Create a discrete_distribution based on the returned weights. This class handles the
            // case where the sum of the weights is < or > 1, by normalizing agains the sum.
            float total = 0f;
            for (int i = 0; i < numWeights; i++)
            {
                if (weights[i] < 0)
                {
                    throw new ArgumentException("Scores must be non-negative.");
                }
                total += weights[i];
            }
            if (total == 0)
            {
                throw new ArgumentException("At least one score must be positive.");
            }

            float draw = random.UniformUnitInterval();

            float sum = 0f;
            float actionProbability = 0f;
            uint actionIndex = numWeights - 1;
            for (int i = 0; i < numWeights; i++)
            {
                weights[i] = weights[i] / total;
                sum += weights[i];
                if (sum > draw)
                {
                    actionIndex = (uint)i;
                    actionProbability = weights[i];
                    break;
                }
            }

            actionIndex++;

            // action id is one-based
            return Decision.Create(
                actionIndex,
                new GenericExplorerState { Probability = actionProbability },
                policyDecision,
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
    public class GenericExplorerSampleWithoutReplacement<TContext, TMapperState> 
        : BaseExplorer<TContext, uint[], GenericExplorerState, float[], TMapperState>
    {
        protected readonly GenericExplorer<TContext, TMapperState> explorer;

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultScorer">A function which outputs the probability of each action.</param>
        /// <param name="numActions">The number of actions to randomize over.</param>
        public GenericExplorerSampleWithoutReplacement(IScorer<TContext, TMapperState> defaultScorer, uint numActions = uint.MaxValue)
             : base(defaultScorer, numActions)
        {
            this.explorer = new GenericExplorer<TContext, TMapperState>(defaultScorer, numActions);
        }

        public override void UpdatePolicy(IContextMapper<TContext, float[], TMapperState> newPolicy)
        {
            base.UpdatePolicy(newPolicy);
            this.explorer.UpdatePolicy(newPolicy);
        }

        public override void EnableExplore(bool explore)
        {
            base.EnableExplore(explore);
            this.explorer.EnableExplore(explore);
        }

        protected override Decision<uint[], GenericExplorerState, float[], TMapperState> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            var random = new PRG(saltedSeed);

            var decision = this.explorer.MapContext(saltedSeed, context, numActionsVariable);

            // Note: this assume update of the weights array.
            float[] weights = decision.MapperDecision.Value;

            float actionProbability = 0f;
            uint[] chosenActions = MultiActionHelper.SampleWithoutReplacement(weights, numActionsVariable, random, ref actionProbability);

            // action id is one-based
            return Decision.Create(chosenActions,
                new GenericExplorerState { Probability = actionProbability },
                decision.MapperDecision,
                true);
        }
    }
}
