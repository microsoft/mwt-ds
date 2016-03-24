using Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    public sealed class EpsilonGreedySlateState
    {
        [JsonProperty(PropertyName = "e")]
        public float Epsilon { get; set; }

        [JsonProperty(PropertyName = "r")]
        public uint[] Ranking { get; set; }

        [JsonProperty(PropertyName = "isExplore")]
        public bool IsExplore { get; set; }
    }

    public sealed class EpsilonGreedySlateExplorer : BaseExplorer<TContext, uint[], TContext, uint[], EpsilonGreedyState, uint, TPolicyState>
    {        
        private readonly float epsilon;

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultPolicy">A default function which outputs an action given a context.</param>
        /// <param name="epsilon">The probability of a random exploration.</param>
        /// <param name="numActions">The number of actions to randomize over.</param>
        public EpsilonGreedySlateExplorer(IPolicy<TContext, uint[], TPolicyState> defaultPolicy, float epsilon, uint numActions = uint.MaxValue)
            : base(defaultPolicy, numActions)
        {
            this.epsilon = epsilon;
        }

        protected override Decision<uint[], EpsilonGreedyState, TPolicyState> ChooseActionInternal(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            // Invoke the default policy function to get the action
            PolicyDecision<uint[], TPolicyState> policyDecisionTuple = this.defaultPolicy.ChooseAction(context, numActions);

            MultiActionHelper.ValidateActionList(policyDecisionTuple.Actions);

            var random = new PRG(saltedSeed);
            float epsilon = explore ? defaultEpsilon : 0f;

            uint[] chosenAction;

            float baseProbability = epsilon / numActions; // uniform probability

            if (random.UniformUnitInterval() < 1f - epsilon)
            {
                chosenAction = Enumerable.Range(0, numActions).Select(u => (uint)u).ToArray();

                for (int i = 0; i < numActions - 1; i++)
			    {
                    int swapIndex = random.UniformInt(i, numActions - 1);

                    uint temp = chosenAction[swapIndex];
                    chosenAction[swapIndex] = chosenAction[i];
                    chosenAction[i] = temp;
			    }
            }
            else
            {
                chosenAction = policyDecisionTuple.Actions;
            }

            EpsilonGreedySlateState explorerState = new EpsilonGreedySlateState 
            { 
                Epsilon = this.epsilon, 
                IsExplore = isExplore,
                Ranking = policyDecisionTuple.Actions
            };

            return Decision.Create(chosenActions, explorerState, policyDecisionTuple.PolicyState);
        }
    }
}
