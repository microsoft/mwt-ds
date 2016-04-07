using System;
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
	public abstract class BaseBootstrapExplorer<TContext, TValue> : IExplorer<TContext, TValue, GenericExplorerState, TValue>
	{
        private IContextMapper<TContext, TValue>[] defaultPolicyFunctions;
        private bool explore;
        private readonly int bags;
	    private readonly int numActionsFixed;

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultPolicies">A set of default policies to be uniform random over.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        protected BaseBootstrapExplorer(IContextMapper<TContext, TValue>[] defaultPolicies, int numActions = int.MaxValue)
		{
            VariableActionHelper.ValidateInitialNumberOfActions(numActions);

            if (defaultPolicies == null || defaultPolicies.Length < 1)
		    {
			    throw new ArgumentException("Number of bags must be at least 1.");
		    }

            this.defaultPolicyFunctions = defaultPolicies;
            this.bags = this.defaultPolicyFunctions.Length;
            this.numActionsFixed = numActions;
            this.explore = true;
        }

        public void UpdatePolicy(IContextMapper<TContext, TValue>[] newPolicies)
        {
            this.defaultPolicyFunctions = newPolicies;
        }

        public void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public Decision<TValue, GenericExplorerState, TValue> MapContext(ulong saltedSeed, TContext context)
        {
            var random = new PRG(saltedSeed);

            // Select bag
            int chosenBag = random.UniformInt(0, this.bags - 1);

            // Invoke the default policy function to get the action
            Decision<TValue> chosenDecision = null; // TODO
            float actionProbability = 0f;

            if (this.explore)
            {
                Decision<TValue> decisionFromBag = null;
                int actionFromBag = 0;
                int[] actionsSelected = Enumerable.Repeat<int>(0, this.numActionsFixed).ToArray();
                // Invoke the default policy function to get the action
                for (int currentBag = 0; currentBag < this.bags; currentBag++)
                {
                    // TODO: can VW predict for all bags on one call? (returning all actions at once)
                    // if we trigger into VW passing an index to invoke bootstrap scoring, and if VW model changes while we are doing so, 
                    // we could end up calling the wrong bag
                    decisionFromBag = this.defaultPolicyFunctions[currentBag].MapContext(context);
                    if (decisionFromBag == null)
                        throw new NotSupportedException("Policy must make a decision.");

                    actionFromBag = this.GetTopAction(decisionFromBag.Value);

                    if (actionFromBag == 0 || actionFromBag > this.numActionsFixed)
                        throw new ArgumentException("Action chosen by default policy is not within valid range.");

                    if (currentBag == chosenBag)
                        chosenDecision = decisionFromBag;

                    //this won't work if actions aren't 0 to Count
                    actionsSelected[actionFromBag - 1]++; // action id is one-based
                }
                actionProbability = (float)actionsSelected[this.GetTopAction(chosenDecision.Value) - 1] / this.bags; // action id is one-based
            }
            else
            {
                chosenDecision = this.defaultPolicyFunctions[0].MapContext(context);
                actionProbability = 1f;
            }

            GenericExplorerState explorerState = new GenericExplorerState
            {
                Probability = actionProbability
            };

            return Decision.Create(chosenDecision.Value, explorerState, chosenDecision, true);
        }

        protected abstract int GetTopAction(TValue action);

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var disposable in this.defaultPolicyFunctions.OfType<IDisposable>())
                    disposable.Dispose();
            }
        }
    }

    public class BootstrapExplorer<TContext> : BaseBootstrapExplorer<TContext, int>
    {
        public BootstrapExplorer(IContextMapper<TContext, int>[] defaultPolicies, 
            int numActions = int.MaxValue)
            : base(defaultPolicies, numActions)
        {
        }

        protected override int GetTopAction(int action)
        {
            return action;
        }
    }

    public class BootstrapTopSlotExplorer<TContext> : BaseBootstrapExplorer<TContext, int[]>
    {
        public BootstrapTopSlotExplorer(IContextMapper<TContext, int[]>[] defaultPolicies,
            int numActions = int.MaxValue)
            : base(defaultPolicies, numActions)
        {
        }

        protected override int GetTopAction(int[] action)
        {
            return action[0];
        }
    }
}