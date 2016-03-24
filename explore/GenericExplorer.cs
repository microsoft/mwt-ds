using Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction;
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
    public class GenericExplorer<TContext, TPolicyState> : BaseExplorer<TContext, uint, GenericExplorerState, float[], TPolicyState>
	{
        private readonly uint numActionsFixed;

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultScorer">A function which outputs the probability of each action.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public GenericExplorer(IPolicy<float[], TContext, TPolicy> defaultScorer, uint numActions = uint.MaxValue) : base(defaultScorer)
		{
            VariableActionHelper.ValidateInitialNumberOfActions(numActions);

            this.numActionsFixed = numActions;
        }

        public Decision<uint, GenericExplorerState, TPolicyState> ChooseAction(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            uint numActions = VariableActionHelper.GetNumberOfActions(this.numActionsFixed, numActionsVariable);

            var random = new PRG(saltedSeed);

            // Invoke the default scorer function
            PolicyDecision<float[], TPolicyState> policyDecision = this.defaultPolicy.ChooseAction(context);
            float[] weights = policyDecision.Action;

            uint numWeights = (uint)weights.Count;
            if (numWeights != numActions)
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

            // action id is one-based
            return Decision.Create(actionIndex + 1,
                new GenericExplorerState { Probability = actionProbability },
                policyDecision);
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
    public class GenericExplorerSampleWithoutReplacement<TContext, TPolicyState> : BaseExplorer<TContext, uint, GenericExplorerState, float[], TPolicyState>
        : IExplorer<TContext, TAction, GenericExplorerState, TPolicyState>, IConsumePolicy<TContext, TAction, TPolicyState>
    {
        protected readonly GenericExplorer<TContext, TPolicyState> explorer;

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultScorer">A function which outputs the probability of each action.</param>
        /// <param name="numActions">The number of actions to randomize over.</param>
        public GenericExplorerSampleWithoutReplacement(IPolicy<TContext, float[], TPolicyState> defaultScorer, uint numActions = uint.MaxValue)
             : base(defaultScorer, numActions)
        {
            this.explorer = new GenericExplorer<TContext, TPolicyState>(defaultScorer, numActions);
        }

        public void UpdatePolicy(IPolicy<TContext, float[], TPolicyState> newPolicy)
        {
            base.UpdatePolicy(newPolicy);
            this.explorer.UpdatePolicy(newPolicy);
        }

        public void EnableExplore(bool explore)
        {
            base.EnableExplore(explore);
            this.explorer.EnableExplore(explore);
        }

        protected override Decision<uint[], GenericExplorerState, TPolicyState> ChooseActionInternal(ulong saltedSeed, TContext context)
        {
            var decision = this.explorer.ChooseAction(saltedSeed, context, numActionsVariable);

            // Note: this assume update of the weights array.
            float[] weights = decision.PolicyDecision.Action;

            float actionProbability = 0f;
            uint[] chosenActions = MultiActionHelper.SampleWithoutReplacement(weights, numActions, random, ref actionProbability);

            // action id is one-based
            return Decision.Create(chosenActions,
                new GenericExplorerState { Probability = actionProbability },
                policyDecision.PolicyState);
        }
    };
}
