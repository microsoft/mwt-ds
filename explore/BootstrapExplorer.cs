using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
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
    public abstract class BaseBootstrapExplorer<TAction> : IExplorer<TAction, IReadOnlyCollection<TAction>>
	{
        private bool explore;

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultPolicies">A set of default policies to be uniform random over.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        protected BaseBootstrapExplorer()
		{
            this.explore = true;
        }

        public void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public ExplorerDecision<TAction> MapContext(PRG random, IReadOnlyCollection<TAction> policyActions, int numActions)
        {
            // Invoke the default policy function to get the action
            TAction chosenDecision = default(TAction);
            float actionProbability = 0f;

            if (this.explore)
            {
                // Select bag
                int chosenBag = random.UniformInt(0, policyActions.Count - 1);

                int[] actionsSelected = Enumerable.Repeat<int>(0, numActions).ToArray();

                int currentBag = 0;
                foreach (var policyAction in policyActions)
                {
                    var actionFromBag = this.GetTopAction(policyAction);

                    if (actionFromBag == 0 || actionFromBag > numActions)
                        throw new ArgumentException("Action chosen by default policy is not within valid range.");

                    //this won't work if actions aren't 0 to Count
                    actionsSelected[actionFromBag - 1]++; // action id is one-based

                    if (currentBag == chosenBag)
                        chosenDecision = policyAction;

                    currentBag++;
                }

                actionProbability = (float)actionsSelected[this.GetTopAction(chosenDecision) - 1] / policyActions.Count; // action id is one-based
            }
            else
            {
                chosenDecision = policyActions.First();
                actionProbability = 1f;
            }

            GenericExplorerState explorerState = new GenericExplorerState
            {
                Probability = actionProbability
            };

            return ExplorerDecision.Create(chosenDecision, explorerState, true);
        }

        protected abstract int GetTopAction(TAction action);
    }

    public class BootstrapExplorer : BaseBootstrapExplorer<int>
    {
        public BootstrapExplorer(int numActions = int.MaxValue)
        {
        }

        protected override int GetTopAction(int action)
        {
            return action;
        }
    }

    public class BootstrapTopSlotExplorer : BaseBootstrapExplorer<int[]>
    {
        public BootstrapTopSlotExplorer(int numActions = int.MaxValue)
        {
        }

        protected override int GetTopAction(int[] action)
        {
            return action[0];
        }
    }
}