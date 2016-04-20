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
    public sealed class SoftmaxExplorer : BaseExplorer<int, float[]>
	{
	    private readonly float lambda;

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultScorer">A function which outputs a score for each action.</param>
		/// <param name="lambda">lambda = 0 implies uniform distribution. Large lambda is equivalent to a max.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public SoftmaxExplorer(float lambda)
        {
            this.lambda = lambda;
        }

        public override ExplorerDecision<int> MapContext(PRG random, float[] scores, int numActions)
        {
            int numScores = scores.Length;
            if (numActions != int.MaxValue && numScores != numActions)
                throw new ArgumentException("The number of scores returned by the scorer must equal number of actions");

            int i = 0;
            float maxScore = scores.Max();

            float actionProbability = 0f;
            int actionIndex = 0;
            if (this.explore)
            {
                // Create a normalized exponential distribution based on the returned scores
                for (i = 0; i < numScores; i++)
                    scores[i] = (float)Math.Exp(this.lambda * (scores[i] - maxScore));

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
                        actionIndex = i;
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
                        actionIndex = i;
                    }
                }
                actionProbability = 1f; // Set to 1 since we always pick the highest one.
            }

            actionIndex++;

            // action id is one-based
            return ExplorerDecision.Create(actionIndex,
                new GenericExplorerState { Probability = actionProbability },
                true);
        }
    }

    public sealed class SoftmaxSampleWithoutReplacementExplorer : BaseExplorer<int[], float[]>
    {
        private readonly SoftmaxExplorer explorer;

		/// <summary>
		/// The constructor is the only public member, because this should be used with the MwtExplorer.
		/// </summary>
		/// <param name="defaultScorer">A function which outputs a score for each action.</param>
		/// <param name="lambda">lambda = 0 implies uniform distribution. Large lambda is equivalent to a max.</param>
		/// <param name="numActions">The number of actions to randomize over.</param>
        public SoftmaxSampleWithoutReplacementExplorer(float lambda)
        {
            this.explorer = new SoftmaxExplorer(lambda);
        }

        public override void EnableExplore(bool explore)
        {
            base.EnableExplore(explore);
            this.explorer.EnableExplore(explore);
        }

        public override ExplorerDecision<int[]> MapContext(PRG random, float[] scores, int numActions)
        {
            if (scores == null || scores.Length < 1)
                throw new ArgumentException("Scores returned by default policy must not be empty.");

            var decision = this.explorer.MapContext(random, scores, numActions);

            int numActionsVariable = scores.Length;

            int[] chosenActions;
            // Note: there might be a way using out generic parameters and explicit interface implementation to avoid the cast
            float actionProbability = ((GenericExplorerState)decision.ExplorerState).Probability;

            if (this.explore)
            {
                chosenActions = MultiActionHelper.SampleWithoutReplacement(scores, numActionsVariable, random, ref actionProbability);
            }
            else
            {
                // avoid linq to optimize perf
                chosenActions = new int[numActionsVariable];
                for (int i = 1; i <= numActionsVariable; i++)
                {
                    chosenActions[i] = i;
                }

                // swap max-score action with the first one
                int firstAction = chosenActions[0];
                chosenActions[0] = chosenActions[decision.Value];
                chosenActions[decision.Value] = firstAction;
            }

            return ExplorerDecision.Create(chosenActions,
                decision.ExplorerState,
                decision.ShouldRecord);
        }
    }
}
