using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    [JsonObject(Id = "stegs")]
    public sealed class EpsilonGreedySlateState
    {
        [JsonProperty(PropertyName = "e")]
        public float Epsilon { get; set; }

        [JsonProperty(PropertyName = "r")]
        public int[] Ranking { get; set; }

        [JsonProperty(PropertyName = "isExplore")]
        public bool IsExplore { get; set; }
    }

    public sealed class EpsilonGreedySlateExplorer : IExplorer<int[], int[]>
    {
        private bool explore;
        private readonly float defaultEpsilon;

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultPolicy">A default function which outputs an action given a context.</param>
        /// <param name="epsilon">The probability of a random exploration.</param>
        public EpsilonGreedySlateExplorer(float epsilon)
        {
            this.defaultEpsilon = epsilon;
            this.explore = true;
        }

        public void EnableExplore(bool explore)
        {
            this.explore = explore;
        }

        public ExplorerDecision<int[]> MapContext(ulong saltedSeed, int[] policyAction)
        {
            MultiActionHelper.ValidateActionList(policyAction);

            float epsilon = this.explore ? this.defaultEpsilon : 0f;

            var random = new PRG(saltedSeed);
            
            int[] chosenAction;
            bool isExplore;

            if (random.UniformUnitInterval() < epsilon)
            {
                // 1 ... n
                chosenAction = Enumerable.Range(1, policyAction.Length).ToArray();

                // 0 ... n - 2
                for (int i = 0; i < policyAction.Length - 1; i++)
			    {
                    int swapIndex = random.UniformInt(i, policyAction.Length - 1);

                    int temp = chosenAction[swapIndex];
                    chosenAction[swapIndex] = chosenAction[i];
                    chosenAction[i] = temp;
			    }

                isExplore = true;
            }
            else
            {
                chosenAction = policyAction;
                isExplore = false;
            }

            EpsilonGreedySlateState explorerState = new EpsilonGreedySlateState 
            { 
                Epsilon = this.defaultEpsilon, 
                IsExplore = isExplore,
                Ranking = policyAction
            };

            return ExplorerDecision.Create(chosenAction, explorerState, true);
        }
    }
}
