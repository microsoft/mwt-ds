using Newtonsoft.Json;
using System;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public sealed class TauFirstState : GenericExplorerState
    {
        [JsonProperty(PropertyName = "t")]
        public uint Tau { get; set; }

        [JsonProperty(PropertyName = "isExplore")]
        public bool IsExplore { get; set; }
    }

    /// <summary>
	/// The tau-first exploration class.
	/// </summary>
	/// <remarks>
	/// The tau-first explorer collects precisely tau uniform random
	/// exploration events, and then uses the default policy. 
	/// </remarks>
	/// <typeparam name="TContext">The Context type.</typeparam>
    public class TauFirstExplorer<TContext, TPolicyState> : BaseExplorer<TContext, uint, TauFirstState, uint, TPolicyState>
	{
        private uint tau;
        private readonly object lockObject = new object();

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultPolicy">A default policy after randomization finishes.</param>
		/// <param name="tau">The number of events to be uniform over.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public TauFirstExplorer(IPolicy<TContext, TPolicyState> defaultPolicy, uint tau, uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.tau = tau;
        }

        protected override Decision<uint, TauFirstState, uint, TPolicyState> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            var random = new PRG(saltedSeed);

            PolicyDecision<uint, TPolicyState> policyDecision= null;
            uint chosenAction = 0;
            float actionProbability = 0f;
            bool shouldRecordDecision;
            bool isExplore = false;
            uint tau = this.tau;

            lock (this.lockObject)
            {
                if (this.tau > 0 && this.explore)
                {
                    this.tau--;
                    uint actionId = random.UniformInt(1, numActionsVariable);
                    actionProbability = 1f / numActionsVariable;
                    chosenAction = actionId;
                    shouldRecordDecision = true;
                    isExplore = true;
                }
                else
                {
                    // Invoke the default policy function to get the action
                    policyDecision = this.defaultPolicy.MapContext(context, numActionsVariable);
                    chosenAction = policyDecision.Action;

                    if (chosenAction == 0 || chosenAction > numActionsVariable)
                    {
                        throw new ArgumentException("Action chosen by default policy is not within valid range.");
                    }

                    actionProbability = 1f;
                    shouldRecordDecision = false;
                }
            }

            TauFirstState explorerState = new TauFirstState
            {
                IsExplore = isExplore,
                Probability = actionProbability,
                Tau = tau
            };

            return Decision.Create(chosenAction, explorerState, policyDecision, shouldRecordDecision);
        }
    }
}