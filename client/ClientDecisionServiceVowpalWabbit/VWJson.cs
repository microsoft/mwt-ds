using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.IO;
using System.Linq;
using VW;
using VW.Serializer;
using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Vowpal Wabbit based policy for string JSON based context.
    /// </summary>
    public class VWJsonPolicy : 
        VWBaseContextMapper<VowpalWabbit, string, int>, 
        IPolicy<string>
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public VWJsonPolicy(Stream vwModelStream = null)
            : base(vwModelStream)
        {
        }

        /// <summary>
        /// Determines the action to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="vw">The Vowpal Wabbit instance to use.</param>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <returns>A decision tuple containing the index of the action to take (1-based), and the Id of the model or policy used to make the decision.
        /// Can be null if the Policy is not ready yet (e.g. model not loaded).</returns>
        protected override PolicyDecision<int> MapContext(VowpalWabbit vw, string context)
        {
            using (var vwJson = new VowpalWabbitJsonSerializer(vw))
            using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context))
            {
                var action = (int)vwExample.Predict(VowpalWabbitPredictionType.CostSensitive);
                var state = new VWState { ModelId = vw.ID };

                return PolicyDecision.Create(action, state);
            }
        }

        /// <summary>
        /// Sub classes must override and create a new VW pool.
        /// </summary>
        protected override VowpalWabbitThreadedPredictionBase<VowpalWabbit> CreatePool(VowpalWabbitSettings settings)
        {
            return new VowpalWabbitThreadedPrediction(settings);
        }
    }

    /// <summary>
    /// Vowpal Wabbit based ranker for string JSON based context.
    /// </summary>
    public class VWJsonRanker : 
        VWBaseContextMapper<VowpalWabbit, string, int[]>, 
        IRanker<string>, INumberOfActionsProvider<string>
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public VWJsonRanker(Stream vwModelStream = null)
            : base(vwModelStream)
        {
        }

        /// <summary>
        /// Sub classes must override and create a new VW pool.
        /// </summary>
        protected override VowpalWabbitThreadedPredictionBase<VowpalWabbit> CreatePool(VowpalWabbitSettings settings)
        {
            return new VowpalWabbitThreadedPrediction(settings);
        }

        /// <summary>
        /// Determines the action to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="vw">The Vowpal Wabbit instance to use.</param>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <returns>A decision tuple containing the index of the action to take (1-based), and the Id of the model or policy used to make the decision.
        /// Can be null if the Policy is not ready yet (e.g. model not loaded).</returns>
        protected override PolicyDecision<int[]> MapContext(VowpalWabbit vw, string context)
        {
            using (var vwJson = new VowpalWabbitJsonSerializer(vw))
            using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context))
            {
                ActionScore[] vwMultilabelPredictions = vwExample.Predict(VowpalWabbitPredictionType.ActionProbabilities);

                // VW multi-label predictions are 0-based
                var actions = vwMultilabelPredictions.Select(a => (int)a.Action + 1).ToArray();
                var state = new VWState { ModelId = vw.ID };

                return PolicyDecision.Create(actions, state);
            }
        }

        /// <summary>
        /// Returns the number of actions defined by this context.
        /// </summary>
        public int GetNumberOfActions(string context)
        {
            return VowpalWabbitJsonSerializer.GetNumberOfActionDependentExamples(context);
        }
    }
}
