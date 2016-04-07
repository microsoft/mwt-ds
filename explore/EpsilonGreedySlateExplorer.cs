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
        public uint[] Ranking { get; set; }

        [JsonProperty(PropertyName = "isExplore")]
        public bool IsExplore { get; set; }
    }

    public sealed class EpsilonGreedySlateExplorer<TContext> : BaseExplorer<TContext, uint[], EpsilonGreedySlateState, uint[]>
    {        
        private readonly float defaultEpsilon;
        private readonly INumberOfActionsProvider<TContext> numberOfActionsProvider;

        /// <summary>
        /// The constructor is the only public member, because this should be used with the MwtExplorer.
        /// </summary>
        /// <param name="defaultPolicy">A default function which outputs an action given a context.</param>
        /// <param name="epsilon">The probability of a random exploration.</param>
        public EpsilonGreedySlateExplorer(IRanker<TContext> defaultPolicy, float epsilon)
            : base(defaultPolicy, uint.MaxValue) // TODO: use int? instead of uint.maxvalue
        {
            this.defaultEpsilon = epsilon;
            this.numberOfActionsProvider = defaultPolicy as INumberOfActionsProvider<TContext>;
        }

        public override Decision<uint[], EpsilonGreedySlateState, uint[]> MapContext(ulong saltedSeed, TContext context)
        {
            float epsilon = this.explore ? this.defaultEpsilon : 0f;
            uint numActionsVariable;

            // Invoke the default policy function to get the action
            Decision<uint[]> policyDecisionTuple = this.contextMapper.MapContext(context);
            if (policyDecisionTuple == null)
            {
                if (this.numberOfActionsProvider == null)
                    throw new InvalidOperationException(string.Format("Ranker '{0}' is unable to provide decision AND does not implement INumberOfActionsProvider", this.contextMapper.GetType()));

                numActionsVariable = (uint)this.numberOfActionsProvider.GetNumberOfActions(context);
                epsilon = 1f;

                policyDecisionTuple = Decision.Create(
                    Enumerable.Range(1, (int)numActionsVariable).Select(a => (uint)a).ToArray());
            }
            else
            {
                numActionsVariable = (uint)policyDecisionTuple.Value.Length; ;
                MultiActionHelper.ValidateActionList(policyDecisionTuple.Value);
            }

            var random = new PRG(saltedSeed);
            
            uint[] chosenAction;
            bool isExplore;

            if (random.UniformUnitInterval() < epsilon)
            {
                // 1 ... n
                chosenAction = Enumerable.Range(1, (int)numActionsVariable).Select(u => (uint)u).ToArray();

                // 0 ... n - 2
                for (int i = 0; i < numActionsVariable - 1; i++)
			    {
                    int swapIndex = (int)random.UniformInt((uint)i, numActionsVariable - 1);

                    uint temp = chosenAction[swapIndex];
                    chosenAction[swapIndex] = chosenAction[i];
                    chosenAction[i] = temp;
			    }

                isExplore = true;
            }
            else
            {
                chosenAction = policyDecisionTuple.Value;
                isExplore = false;
            }

            EpsilonGreedySlateState explorerState = new EpsilonGreedySlateState 
            { 
                Epsilon = this.defaultEpsilon, 
                IsExplore = isExplore,
                Ranking = policyDecisionTuple.Value
            };

            return Decision.Create(chosenAction, explorerState, policyDecisionTuple, true);
        }
    }
}
