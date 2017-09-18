using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VW;
using VW.Serializer;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Vowpal Wabbit based explorer using C# based features.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public sealed class VWExplorer<TContext> :
        VWBaseContextMapper<VowpalWabbit<TContext>, TContext, ActionProbability[]>,
        IContextMapper<TContext, ActionProbability[]>, INumberOfActionsProvider<TContext>
    {
        private readonly IVowpalWabbitSerializerCompiler<TContext> serializer;
        private readonly IVowpalWabbitMultiExampleSerializerCompiler<TContext> multiSerializer;

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        public VWExplorer(Stream vwModelStream = null, ITypeInspector typeInspector = null, bool developmentMode = false)
            : base(vwModelStream, typeInspector, developmentMode)
        {
            this.serializer = VowpalWabbitSerializerFactory.CreateSerializer<TContext>(new VowpalWabbitSettings 
            { 
                TypeInspector = this.typeInspector,
                EnableStringExampleGeneration = this.developmentMode,
                EnableStringFloatCompact = this.developmentMode
            });

            this.multiSerializer = this.serializer as IVowpalWabbitMultiExampleSerializerCompiler<TContext>;
        }

        /// <summary>
        /// Sub classes must override and create a new VW pool.
        /// </summary>
        protected override VowpalWabbitThreadedPredictionBase<VowpalWabbit<TContext>> CreatePool(VowpalWabbitSettings settings)
        {
            return new VowpalWabbitThreadedPrediction<TContext>(settings);
        }

        /// <summary>
        /// Determines the action to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="vw">The Vowpal Wabbit instance to use.</param>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <returns>A decision tuple containing the index of the action to take (1-based), and the Id of the model or policy used to make the decision.
        /// Can be null if the Policy is not ready yet (e.g. model not loaded).</returns>
        protected override PolicyDecision<ActionProbability[]> MapContext(VowpalWabbit<TContext> vw, TContext context)
        {
            if (this.developmentMode)
            {
                using (var serializer = vw.Serializer.Create(vw.Native))
                {
                    Trace.TraceInformation("Example Context: {0}", serializer.SerializeToString(context));
                }
            }

            var vwPredictions = vw.Predict(context, VowpalWabbitPredictionType.ActionProbabilities);

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

        /// <summary>
        /// Returns the number of actions defined by this context.
        /// </summary>
        public int GetNumberOfActions(TContext context)
        {
            if (this.multiSerializer == null)
                throw new NotSupportedException("The serializer for " + typeof(TContext) + " is not a multi example serializer");

            return this.multiSerializer.GetNumberOfActionDependentExamples(context);
        }
    }
}
