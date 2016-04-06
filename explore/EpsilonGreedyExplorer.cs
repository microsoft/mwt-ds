using Newtonsoft.Json;
using System;
using System.Reflection;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    [JsonObject(Id = "steg")] // TODO: do this for all of the other explorer states
    public sealed class EpsilonGreedyState : GenericExplorerState
    {
        [JsonProperty(PropertyName = "e")]
        public float Epsilon { get; set; }
        
        [JsonProperty(PropertyName = "isExplore")]
        public bool IsExplore { get; set; }
    }

    /// <summary>
    /// The epsilon greedy exploration class.
    /// </summary>
    /// <remarks>
    /// This is a good choice if you have no idea which actions should be preferred.
    /// Epsilon greedy is also computationally cheap.
    /// </remarks>
    /// <typeparam name="TContext">The Context type.</typeparam>
    public class EpsilonGreedyExplorer<TContext> : BaseVariableActionExplorer<TContext, uint, EpsilonGreedyState, uint>
    {
        private readonly float defaultEpsilon;

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultPolicy">A default function which outputs an action given a context.</param>
        /// <param name="epsilon">The probability of a random exploration.</param>
        /// <param name="numActions">The number of actions to randomize over.</param>
        public EpsilonGreedyExplorer(IContextMapper<TContext, uint> defaultPolicy, float epsilon, uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            if (epsilon < 0 || epsilon > 1)
            {
                throw new ArgumentException("Epsilon must be between 0 and 1.");
            }
            this.defaultEpsilon = epsilon;
        }

        protected override Decision<uint, EpsilonGreedyState, uint> MapContext(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            // Invoke the default policy function to get the action
            Decision<uint> policyDecisionTuple = this.contextMapper.MapContext(context);
            uint chosenAction = policyDecisionTuple.Value;

            if (chosenAction == 0 || chosenAction > numActionsVariable)
            {
                throw new ArgumentException("Action chosen by default policy is not within valid range.");
            }

            float actionProbability;
            bool isExplore;

            var random = new PRG(saltedSeed);
            float epsilon = explore ? this.defaultEpsilon : 0f;

            float baseProbability = epsilon / numActionsVariable; // uniform probability

            if (random.UniformUnitInterval() < 1f - epsilon)
            {
                actionProbability = 1f - epsilon + baseProbability;
                isExplore = false;
            }
            else
            {
                // Get uniform random 1-based action ID
                uint actionId = (uint)random.UniformInt(1, numActionsVariable);

                if (actionId == chosenAction)
                {
                    // If it matches the one chosen by the default policy
                    // then increase the probability
                    actionProbability = 1f - epsilon + baseProbability;
                }
                else
                {
                    // Otherwise it's just the uniform probability
                    actionProbability = baseProbability;
                }
                chosenAction = actionId;
                isExplore = true;
            }

            EpsilonGreedyState explorerState = new EpsilonGreedyState 
            { 
                Epsilon = epsilon, 
                IsExplore = isExplore,
                Probability = actionProbability
            };

            return Decision.Create(chosenAction, explorerState, policyDecisionTuple, true);
        }
    }
}