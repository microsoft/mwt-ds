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
    public sealed class SoftmaxExplorer<TContext, TMapperState> : BaseExplorer<TContext, uint, GenericExplorerState, float[], TMapperState>
	{
	    private readonly float lambda;

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultScorer">A function which outputs a score for each action.</param>
		/// <param name="lambda">lambda = 0 implies uniform distribution. Large lambda is equivalent to a max.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public SoftmaxExplorer(IScorer<TContext, TMapperState> defaultScorer, float lambda, uint numActions = uint.MaxValue)
            : base(defaultScorer, numActions)
        {
            this.lambda = lambda;
        }

        protected override Decision<uint, GenericExplorerState, float[], TMapperState> MapContextInternal(ulong saltedSeed, TContext context, uint numActionsVariable)
        {
            var random = new PRG(saltedSeed);

            // Invoke the default scorer function
            Decision<float[], TMapperState> policyDecision= this.defaultPolicy.MapContext(context);
            float[] scores = policyDecision.Value;
            uint numScores = (uint)scores.Length;
            if (numScores != numActionsVariable)
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
}
