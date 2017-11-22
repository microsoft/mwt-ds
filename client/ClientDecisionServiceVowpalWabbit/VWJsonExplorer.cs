using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VW;
using VW.Serializer;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Vowpal Wabbit based explorer for JSON context.
    /// </summary>
    public sealed class VWJsonExplorer :
        VWBaseContextMapper<VowpalWabbit, string, ActionProbability[]>, 
        IContextMapper<string, ActionProbability[]>, INumberOfActionsProvider<string>
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public VWJsonExplorer(Stream vwModelStream = null, bool developmentMode = false)
            : base(vwModelStream, developmentMode: developmentMode)
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
        protected override PolicyDecision<ActionProbability[]> MapContext(VowpalWabbit vw, string context)
        {
            using (var vwJson = new VowpalWabbitJsonSerializer(vw))
            using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(context))
            {
                if (this.developmentMode)
                    Trace.TraceInformation("Example Context: '{0}'", vwExample.VowpalWabbitString);

                var vwPredictions = vwExample.Predict(VowpalWabbitPredictionType.ActionProbabilities);

                // VW multi-label predictions are 0-based
                var ap = vwPredictions
                    .Select(a => 
                        new ActionProbability
                        { 
                            Action = (int)(a.Action + 1),
                            Probability = a.Score
                        })
                        .ToArray();
                var state = new VWState { ModelId = vw.Native.ID };

                return PolicyDecision.Create(ap, state);
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
