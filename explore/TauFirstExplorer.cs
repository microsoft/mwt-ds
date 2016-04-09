using Newtonsoft.Json;
using System;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    [JsonObject(Id = "sttf")]
    public sealed class TauFirstState : GenericExplorerState
    {
        [JsonProperty(PropertyName = "t")]
        public int Tau { get; set; }

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
    public sealed class TauFirstExplorer<TContext> : BaseVariableActionExplorer<int, int>
	{
        private int tau;
        private readonly object lockObject = new object();

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultPolicy">A default policy after randomization finishes.</param>
		/// <param name="tau">The number of events to be uniform over.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public TauFirstExplorer(int tau, int numActions = int.MaxValue)
            : base(numActions)
        {
            this.tau = tau;
        }

        public override ExplorerDecision<int> Explore(ulong saltedSeed, int policyAction, int numActionsVariable)
        {
            if (policyAction == 0 || policyAction > numActionsVariable)
                throw new ArgumentException("Action chosen by default policy is not within valid range.");

            int chosenAction = 0;
            float actionProbability = 0f;
            bool shouldRecordDecision = true;
            bool isExplore = true;
            int tau = this.tau;

            lock (this.lockObject)
            {
                if (this.tau > 0 && this.explore)
                {
                    this.tau--;

                    var random = new PRG(saltedSeed);
                    chosenAction = random.UniformInt(1, numActionsVariable);
                    actionProbability = 1f / numActionsVariable;
                    isExplore = true;
                }
                else
                {
                    chosenAction = policyAction;

                    actionProbability = 1f;
                    shouldRecordDecision = false; // TODO: don't?
                    isExplore = false;
                }
            }

            TauFirstState explorerState = new TauFirstState
            {
                IsExplore = isExplore,
                Probability = actionProbability,
                Tau = tau
            };

            return ExplorerDecision.Create(chosenAction, explorerState, shouldRecordDecision);
        }
    }
}