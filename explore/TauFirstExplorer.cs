using System;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction
{
    /// <summary>
	/// The tau-first exploration class.
	/// </summary>
	/// <remarks>
	/// The tau-first explorer collects precisely tau uniform random
	/// exploration events, and then uses the default policy. 
	/// </remarks>
	/// <typeparam name="TContext">The Context type.</typeparam>
    public class TauFirstExplorer<TContext> : IExplorer<TContext>, IConsumePolicy<TContext>
	{
        private IPolicy<TContext> defaultPolicy;
        private uint tau;
        private bool explore;
        private readonly uint numActionsFixed;

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultPolicy">A default policy after randomization finishes.</param>
		/// <param name="tau">The number of events to be uniform over.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public TauFirstExplorer(IPolicy<TContext> defaultPolicy, uint tau, uint numActions)
        {
            VariableActionHelper.ValidateInitialNumberOfActions(numActions);

            this.defaultPolicy = defaultPolicy;
            this.tau = tau;
            this.numActionsFixed = numActions;
            this.explore = true;
        }

        /// <summary>
        /// Initializes a tau-first explorer with variable number of actions.
        /// </summary>
        /// <param name="defaultPolicy">A default policy after randomization finishes.</param>
        /// <param name="tau">The number of events to be uniform over.</param>
        public TauFirstExplorer(IPolicy<TContext> defaultPolicy, uint tau) :
            this(defaultPolicy, tau, uint.MaxValue)
        { }

        public void UpdatePolicy(IPolicy<TContext> newPolicy)
        {
            this.defaultPolicy = newPolicy;
        }

        public void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public DecisionTuple ChooseAction(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            uint numActions = VariableActionHelper.GetNumberOfActions(this.numActionsFixed, numActionsVariable);

            var random = new PRG(saltedSeed);

            PolicyDecisionTuple policyDecisionTuple = null;
            uint chosenAction = 0;
            float actionProbability = 0f;
            bool shouldRecordDecision;
            bool isExplore = false;

            if (this.tau > 0 && this.explore)
            {
                this.tau--;
                uint actionId = random.UniformInt(1, numActions);
                actionProbability = 1f / numActions;
                chosenAction = actionId;
                shouldRecordDecision = true;
                isExplore = true;
            }
            else
            {
                // Invoke the default policy function to get the action
                policyDecisionTuple = this.defaultPolicy.ChooseAction(context, numActionsVariable);
                chosenAction = policyDecisionTuple.Action;

                if (chosenAction == 0 || chosenAction > numActions)
                {
                    throw new ArgumentException("Action chosen by default policy is not within valid range.");
                }

                actionProbability = 1f;
                shouldRecordDecision = false;
            }
            return new DecisionTuple
            {
                Action = chosenAction,
                Probability = actionProbability,
                ShouldRecord = shouldRecordDecision,
                ModelId = policyDecisionTuple != null ? policyDecisionTuple.ModelId : null,
                IsExplore = isExplore
            };
        }
    };
}

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction
{
    /// <summary>
    /// The tau-first exploration class.
    /// </summary>
    /// <remarks>
    /// The tau-first explorer collects precisely tau uniform random
    /// exploration events, and then uses the default policy. 
    /// </remarks>
    /// <typeparam name="TContext">The Context type.</typeparam>
    public class TauFirstExplorer<TContext> : IExplorer<TContext>, IConsumePolicy<TContext>
    {
        private IPolicy<TContext> defaultPolicy;
        private uint tau;
        private bool explore;
        private readonly uint numActionsFixed;
        private readonly object lockObject = new object();

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultPolicy">A default policy after randomization finishes.</param>
        /// <param name="tau">The number of events to be uniform over.</param>
        /// <param name="numActions">The number of actions to randomize over.</param>
        public TauFirstExplorer(IPolicy<TContext> defaultPolicy, uint tau, uint numActions)
        {
            VariableActionHelper.ValidateInitialNumberOfActions(numActions);

            this.defaultPolicy = defaultPolicy;
            this.tau = tau;
            this.numActionsFixed = numActions;
            this.explore = true;
        }

        /// <summary>
        /// Initializes a tau-first explorer with variable number of actions.
        /// </summary>
        /// <param name="defaultPolicy">A default policy after randomization finishes.</param>
        /// <param name="tau">The number of events to be uniform over.</param>
        public TauFirstExplorer(IPolicy<TContext> defaultPolicy, uint tau) :
            this(defaultPolicy, tau, uint.MaxValue)
        { }

        public void UpdatePolicy(IPolicy<TContext> newPolicy)
        {
            this.defaultPolicy = newPolicy;
        }

        public void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public DecisionTuple ChooseAction(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            uint numActions = VariableActionHelper.GetNumberOfActions(this.numActionsFixed, numActionsVariable);

            var random = new PRG(saltedSeed);

            float actionProbability = 0f;
            bool shouldRecordDecision;

            
            PolicyDecisionTuple policyDecisionTuple = this.defaultPolicy.ChooseAction(context, numActionsVariable);
            uint[] chosenActions = policyDecisionTuple.Actions;
            MultiActionHelper.ValidateActionList(chosenActions);

            bool explore = false;
            if (this.explore)
            {
                lock (lockObject)
                {
                    if (this.tau > 0)
                    {
                        this.tau--;
                        explore = true;
                    }
                }
            }

            if (explore)
            {
                uint topAction = random.UniformInt(1, numActions);
                actionProbability = 1f / numActions;

                MultiActionHelper.PutActionToList(topAction, chosenActions);

                shouldRecordDecision = true;
            }
            else
            {
                actionProbability = 1f;
                shouldRecordDecision = false;
            }
            return new DecisionTuple
            {
                Actions = chosenActions,
                Probability = actionProbability,
                ShouldRecord = shouldRecordDecision,
                ModelId = policyDecisionTuple.ModelId,
                IsExplore = explore
            };
        }
    };
}