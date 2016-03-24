using System;
using System.Linq;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction
{
    /// <summary>
	/// The bootstrap exploration class.
	/// </summary>
	/// <remarks>
	/// The Bootstrap explorer randomizes over the actions chosen by a set of
	/// default policies.  This performs well statistically but can be
	/// computationally expensive.
	/// </remarks>
	/// <typeparam name="TContext">The Context type.</typeparam>
	public class BootstrapExplorer<TContext> : IExplorer<TContext, TAction, TExplorerState, TPolicyState>, IConsumePolicies<TContext, TPolicyAction, TPolicyState>
	{
        private IPolicy<TContext, TPolicyAction, TPolicyState>[] defaultPolicyFunctions;
        private bool explore;
        private readonly uint bags;
	    private readonly uint numActionsFixed;

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultPolicies">A set of default policies to be uniform random over.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public BootstrapExplorer(IPolicy<TContext, TPolicyAction, TPolicyState>[] defaultPolicies, uint numActions)
		{
            VariableActionHelper.ValidateInitialNumberOfActions(numActions);

            if (defaultPolicies == null || defaultPolicies.Length < 1)
		    {
			    throw new ArgumentException("Number of bags must be at least 1.");
		    }

            this.defaultPolicyFunctions = defaultPolicies;
            this.bags = (uint)this.defaultPolicyFunctions.Length;
            this.numActionsFixed = numActions;
            this.explore = true;
        }

        /// <summary>
        /// Initializes a bootstrap explorer with variable number of actions.
        /// </summary>
        /// <param name="defaultPolicies">A set of default policies to be uniform random over.</param>
        public BootstrapExplorer(IPolicy<TContext>[] defaultPolicies) :
            this(defaultPolicies, uint.MaxValue)
        { }

        public void UpdatePolicy(IPolicy<TContext>[] newPolicies)
        {
            this.defaultPolicyFunctions = newPolicies;
        }

        public void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public DecisionTuple ChooseAction(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            uint numActions = VariableActionHelper.GetNumberOfActions(this.numActionsFixed, numActionsVariable);

            var random = new PRG(saltedSeed);

            // Select bag
            uint chosenBag = random.UniformInt(0, this.bags - 1);

            // Invoke the default policy function to get the action
            var chosenDecision = new PolicyDecisionTuple();
            float actionProbability = 0f;

            if (this.explore)
            {
                PolicyDecisionTuple decisionFromBag = null;
                uint actionFromBag = 0;
                uint[] actionsSelected = Enumerable.Repeat<uint>(0, (int)numActions).ToArray();

                // Invoke the default policy function to get the action
                for (int currentBag = 0; currentBag < this.bags; currentBag++)
                {
                    // TODO: can VW predict for all bags on one call? (returning all actions at once)
                    // if we trigger into VW passing an index to invoke bootstrap scoring, and if VW model changes while we are doing so, 
                    // we could end up calling the wrong bag
                    decisionFromBag = this.defaultPolicyFunctions[currentBag].ChooseAction(context, numActionsVariable);
                    actionFromBag = decisionFromBag.Action;

                    if (actionFromBag == 0 || actionFromBag > numActions)
                    {
                        throw new ArgumentException("Action chosen by default policy is not within valid range.");
                    }

                    if (currentBag == chosenBag)
                    {
                        chosenDecision.Action = actionFromBag;
                        chosenDecision.ModelId = decisionFromBag.ModelId;
                    }
                    //this won't work if actions aren't 0 to Count
                    actionsSelected[actionFromBag - 1]++; // action id is one-based
                }
                actionProbability = (float)actionsSelected[chosenDecision.Action - 1] / this.bags; // action id is one-based
            }
            else
            {
                chosenDecision = this.defaultPolicyFunctions[0].ChooseAction(context, numActionsVariable);
                actionProbability = 1f;
            }
            return new DecisionTuple
            {
                Action = chosenDecision.Action,
                Probability = actionProbability,
                ShouldRecord = true,
                ModelId = chosenDecision.ModelId
            };
        }
    };
}

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction
{
    /// <summary>
    /// The bootstrap exploration class.
    /// </summary>
    /// <remarks>
    /// The Bootstrap explorer randomizes over the actions chosen by a set of
    /// default policies.  This performs well statistically but can be
    /// computationally expensive.
    /// </remarks>
    /// <typeparam name="TContext">The Context type.</typeparam>
    public class BootstrapExplorer<TContext> : IExplorer<TContext>, IConsumePolicies<TContext>
    {
        private IPolicy<TContext>[] defaultPolicyFunctions;
        private bool explore;
        private readonly uint bags;
        private readonly uint numActionsFixed;

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultPolicies">A set of default policies to be uniform random over.</param>
        /// <param name="numActions">The number of actions to randomize over.</param>
        public BootstrapExplorer(IPolicy<TContext>[] defaultPolicies, uint numActions)
        {
            VariableActionHelper.ValidateInitialNumberOfActions(numActions);

            if (defaultPolicies == null || defaultPolicies.Length < 1)
            {
                throw new ArgumentException("Number of bags must be at least 1.");
            }

            this.defaultPolicyFunctions = defaultPolicies;
            this.bags = (uint)this.defaultPolicyFunctions.Length;
            this.numActionsFixed = numActions;
            this.explore = true;
        }

        /// <summary>
        /// Initializes a bootstrap explorer with variable number of actions.
        /// </summary>
        /// <param name="defaultPolicies">A set of default policies to be uniform random over.</param>
        public BootstrapExplorer(IPolicy<TContext>[] defaultPolicies) :
            this(defaultPolicies, uint.MaxValue)
        { }

        public void UpdatePolicy(IPolicy<TContext>[] newPolicies)
        {
            this.defaultPolicyFunctions = newPolicies;
        }

        public void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public DecisionTuple ChooseAction(ulong saltedSeed, TContext context, uint numActionsVariable = uint.MaxValue)
        {
            uint numActions = VariableActionHelper.GetNumberOfActions(this.numActionsFixed, numActionsVariable);

            var random = new PRG(saltedSeed);

            // Select bag
            uint chosenBag = random.UniformInt(0, this.bags - 1);

            // Invoke the default policy function to get the action
            var chosenDecision = new PolicyDecisionTuple();
            uint chosenTopAction = 0;
            float actionProbability = 0f;

            if (this.explore)
            {
                uint topActionFromBag = 0;
                PolicyDecisionTuple decisionFromBag = null;
                uint[] actionsSelected = Enumerable.Repeat<uint>(0, (int)numActions).ToArray();

                // Invoke the default policy function to get the action
                for (int currentBag = 0; currentBag < this.bags; currentBag++)
                {
                    // TODO: can VW predict for all bags on one call? (returning all actions at once)
                    // if we trigger into VW passing an index to invoke bootstrap scoring, and if VW model changes while we are doing so, 
                    // we could end up calling the wrong bag
                    decisionFromBag = this.defaultPolicyFunctions[currentBag].ChooseAction(context, numActions);
                    uint[] actionsFromBag = decisionFromBag.Actions;

                    MultiActionHelper.ValidateActionList(actionsFromBag);

                    topActionFromBag = actionsFromBag[0];

                    if (currentBag == chosenBag)
                    {
                        chosenTopAction = topActionFromBag;
                        chosenDecision.Actions = actionsFromBag;
                        chosenDecision.ModelId = decisionFromBag.ModelId;
                    }
                    //this won't work if actions aren't 0 to Count
                    actionsSelected[topActionFromBag - 1]++; // action id is one-based
                }
                actionProbability = (float)actionsSelected[chosenTopAction - 1] / this.bags; // action id is one-based
            }
            else
            {
                chosenDecision = this.defaultPolicyFunctions[0].ChooseAction(context, numActions);
                actionProbability = 1f;
            }
            return new DecisionTuple
            {
                Actions = chosenDecision.Actions,
                Probability = actionProbability,
                ShouldRecord = true,
                ModelId = chosenDecision.ModelId
            };
        }
    };
}