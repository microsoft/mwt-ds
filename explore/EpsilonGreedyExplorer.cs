using Newtonsoft.Json;
using System;
using System.Reflection;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public sealed class EpsilonGreedyState : GenericExplorerState
    {
        [JsonProperty(PropertyName = "e")]
        public float Epsilon { get; set; }
        
        [JsonProperty(PropertyName = "isExplore")]
        public bool IsExplore { get; set; }
    }

    public static class EpsilonGreedy
    {
        // TODO: move me down
        public static void Explore(ref uint outAction, ref float outProbability, ref bool outShouldLog, ref bool outIsExplore,
            uint numActions, bool explore, float defaultEpsilon, ulong saltedSeed)
        {
            var random = new PRG(saltedSeed);
            float epsilon = explore ? defaultEpsilon : 0f;

            float baseProbability = epsilon / numActions; // uniform probability

            if (random.UniformUnitInterval() < 1f - epsilon)
            {
                outProbability = 1f - epsilon + baseProbability;
            }
            else
            {
                // Get uniform random 1-based action ID
                uint actionId = (uint)random.UniformInt(1, numActions);

                if (actionId == outAction)
                {
                    // If it matches the one chosen by the default policy
                    // then increase the probability
                    outProbability = 1f - epsilon + baseProbability;
                }
                else
                {
                    // Otherwise it's just the uniform probability
                    outProbability = baseProbability;
                }
                outAction = actionId;
                outIsExplore = true;
            }

            outShouldLog = true;
        }
    }

    /// <summary>
    /// The epsilon greedy exploration class.
    /// </summary>
    /// <remarks>
    /// This is a good choice if you have no idea which actions should be preferred.
    /// Epsilon greedy is also computationally cheap.
    /// </remarks>
    /// <typeparam name="TContext">The Context type.</typeparam>
    public class EpsilonGreedyExplorer<TContext, TPolicyState> : BaseExplorer<TContext, uint, EpsilonGreedyState, uint, TPolicyState>
    {
        private readonly float epsilon;

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultPolicy">A default function which outputs an action given a context.</param>
        /// <param name="epsilon">The probability of a random exploration.</param>
        /// <param name="numActions">The number of actions to randomize over.</param>
        public EpsilonGreedyExplorer(IPolicy<TContext, uint, TPolicyState> defaultPolicy, float epsilon, uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            if (epsilon < 0 || epsilon > 1)
            {
                throw new ArgumentException("Epsilon must be between 0 and 1.");
            }
            this.epsilon = epsilon;
        }

        protected override Decision<uint, EpsilonGreedyState, TPolicyState> ChooseActionInternal(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            // Invoke the default policy function to get the action
            PolicyDecision<uint, TPolicyState> policyDecisionTuple = this.defaultPolicy.ChooseAction(context, numActionsVariable);
            uint chosenAction = policyDecisionTuple.Action;

            if (chosenAction == 0 || chosenAction > numActionsVariable)
            {
                throw new ArgumentException("Action chosen by default policy is not within valid range.");
            }

            float actionProbability = 0f;
            bool shouldRecord = false;
            bool isExplore = false;

            EpsilonGreedy.Explore(ref chosenAction, ref actionProbability, ref shouldRecord, ref isExplore,
                numActionsVariable, this.explore, this.epsilon, saltedSeed);

            EpsilonGreedyState explorerState = new EpsilonGreedyState 
            { 
                Epsilon = this.epsilon, 
                IsExplore = isExplore,
                Probability = actionProbability
            };

            return Decision.Create(chosenAction, explorerState, policyDecisionTuple.PolicyState, true);
        }
    }
}