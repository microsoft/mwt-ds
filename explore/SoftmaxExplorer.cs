using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Research.MultiWorldTesting.ExploreLibrary
{
    /// <summary>
	/// The softmax exploration class.
	/// </summary>
	/// <remarks>
	/// In some cases, different actions have a different scores, and you
	/// would prefer to choose actions with large scores. Softmax allows 
	/// you to do that.
	/// </remarks>
	/// <typeparam name="TContext">The Context type.</typeparam>
    public sealed class SoftmaxExplorer<TContext> : BaseExplorer<TContext, uint, GenericExplorerState, float[]>
	{
	    private readonly float lambda;

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultScorer">A function which outputs a score for each action.</param>
		/// <param name="lambda">lambda = 0 implies uniform distribution. Large lambda is equivalent to a max.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public SoftmaxExplorer(IContextMapper<TContext, float[]> defaultScorer, float lambda, uint numActions)
            : base(defaultScorer, numActions)
        {
            this.lambda = lambda;
        }

        public override Decision<uint, GenericExplorerState, float[]> MapContext(ulong saltedSeed, TContext context)
        {
            var random = new PRG(saltedSeed);

            // Invoke the default scorer function
            Decision<float[]> policyDecision = this.contextMapper.MapContext(context);
            float[] scores = policyDecision.Value;
            uint numScores = (uint)scores.Length;
            if (this.numActionsFixed != uint.MaxValue && numScores != this.numActionsFixed)
            {
                throw new ArgumentException("The number of scores returned by the scorer must equal number of actions");
            }

            int i = 0;

            float maxScore = scores.Max();

            float actionProbability = 0f;
            uint actionIndex = 0;
            if (this.explore)
            {
                // Create a normalized exponential distribution based on the returned scores
                for (i = 0; i < numScores; i++)
                {
                    scores[i] = (float)Math.Exp(this.lambda * (scores[i] - maxScore));
                }

                // Create a discrete_distribution based on the returned weights. This class handles the
                // case where the sum of the weights is < or > 1, by normalizing agains the sum.
                float total = scores.Sum();

                float draw = random.UniformUnitInterval();

                float sum = 0f;
                actionProbability = 0f;
                actionIndex = numScores - 1;
                for (i = 0; i < numScores; i++)
                {
                    scores[i] = scores[i] / total;
                    sum += scores[i];
                    if (sum > draw)
                    {
                        actionIndex = (uint)i;
                        actionProbability = scores[i];
                        break;
                    }
                }
            }
            else
            {
                maxScore = 0f;
                for (i = 0; i < numScores; i++)
                {
                    if (maxScore < scores[i])
                    {
                        maxScore = scores[i];
                        actionIndex = (uint)i;
                    }
                }
                actionProbability = 1f; // Set to 1 since we always pick the highest one.
            }

            actionIndex++;

            // action id is one-based
            return Decision.Create(actionIndex,
                new GenericExplorerState { Probability = actionProbability },
                policyDecision,
                true);
        }
    }

    public sealed class SoftmaxSampleWithoutReplacementExplorer<TContext>
        : BaseExplorer<TContext, uint[], GenericExplorerState, float[]>
    {
        private readonly SoftmaxExplorer<TContext> explorer;

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultScorer">A function which outputs a score for each action.</param>
		/// <param name="lambda">lambda = 0 implies uniform distribution. Large lambda is equivalent to a max.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public SoftmaxSampleWithoutReplacementExplorer(IContextMapper<TContext, float[]> defaultScorer, float lambda)
            : base(defaultScorer, uint.MaxValue)
        {
            this.explorer = new SoftmaxExplorer<TContext>(defaultScorer, lambda, uint.MaxValue);
        }

        public override void EnableExplore(bool explore)
        {
            base.EnableExplore(explore);
            this.explorer.EnableExplore(explore);
        }

        public override Decision<uint[], GenericExplorerState, float[]> MapContext(ulong saltedSeed, TContext context)
        {
            var decision = this.explorer.MapContext(saltedSeed, context);
            var scores = decision.MapperDecision.Value;
            uint numActionsVariable = (uint)scores.Length;
            // TODO: check scores null or empty array

            uint[] chosenActions;
            float actionProbability = decision.ExplorerState.Probability;

            if (this.explore)
            {
                var random = new PRG(saltedSeed);
                chosenActions = MultiActionHelper.SampleWithoutReplacement(scores, numActionsVariable, random, ref actionProbability);
            }
            else
            {
                // avoid linq to optimize perf
                chosenActions = new uint[numActionsVariable];
                for (int i = 1; i <= numActionsVariable; i++)
                {
                    chosenActions[i] = (uint)i;
                }

                // swap max-score action with the first one
                uint firstAction = chosenActions[0];
                chosenActions[0] = chosenActions[decision.Value];
                chosenActions[decision.Value] = firstAction;
            }

            return Decision.Create(chosenActions,
                decision.ExplorerState,
                decision.MapperDecision,
                decision.ShouldRecord);
        }
    }
}
