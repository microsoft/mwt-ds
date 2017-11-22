using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VW;
using VW.Serializer;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Vowpal Wabbit based ranker.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public class VWRanker<TContext> :
        VWBaseContextMapper<VowpalWabbit<TContext>, TContext, int[]>,
        IRanker<TContext>, INumberOfActionsProvider<TContext>
    {
        private readonly IVowpalWabbitMultiExampleSerializerCompiler<TContext> serializer;

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        public VWRanker(Stream vwModelStream = null, ITypeInspector typeInspector = null, bool developmentMode = false)
            : base(vwModelStream, typeInspector, developmentMode)
        {
            this.serializer = VowpalWabbitSerializerFactory.CreateSerializer<TContext>(new VowpalWabbitSettings 
            { 
                TypeInspector = this.typeInspector,
                EnableStringExampleGeneration = this.developmentMode,
                EnableStringFloatCompact = this.developmentMode
            }) as IVowpalWabbitMultiExampleSerializerCompiler<TContext>;
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
        protected override PolicyDecision<int[]> MapContext(VowpalWabbit<TContext> vw, TContext context)
        {
            if (this.developmentMode)
            {
                using (var serializer = vw.Serializer.Create(vw.Native))
                {
                    Trace.TraceInformation("Example Context: {0}", serializer.SerializeToString(context));
                }
            }

            ActionScore[] vwMultilabelPredictions = vw.Predict(context, VowpalWabbitPredictionType.ActionProbabilities);

            // VW multi-label predictions are 0-based
            var actions = vwMultilabelPredictions.Select(a => (int)a.Action + 1).ToArray();
            var state = new VWState { ModelId = vw.Native.ID };

            return PolicyDecision.Create(actions, state);
        }

        /// <summary>
        /// Returns the number of actions defined by this context.
        /// </summary>

        public int GetNumberOfActions(TContext context)
        {
            return this.serializer.GetNumberOfActionDependentExamples(context);
        }
    }

    /// <summary>
    /// Vowpal Wabbit based ranker using C# defined context and action dependent features.
    /// </summary>
    public class VWRanker<TContext, TActionDependentFeature> :
        VWBaseContextMapper<VowpalWabbit<TContext, TActionDependentFeature>, TContext, int[]>,
        IRanker<TContext>, INumberOfActionsProvider<TContext>
    {
        private readonly Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc;

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        public VWRanker(
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            Stream vwModelStream = null,
            ITypeInspector typeInspector = null,
            bool developmentMode = false)
            : base(vwModelStream, typeInspector, developmentMode)
        {
            this.getContextFeaturesFunc = getContextFeaturesFunc;
        }

        /// <summary>
        /// Sub classes must override and create a new VW pool.
        /// </summary>
        protected override VowpalWabbitThreadedPredictionBase<VowpalWabbit<TContext, TActionDependentFeature>> CreatePool(VowpalWabbitSettings settings)
        {
            return new VowpalWabbitThreadedPrediction<TContext, TActionDependentFeature>(settings);
        }

        /// <summary>
        /// Determines the action to take for a given context.
        /// This implementation should be thread-safe if multithreading is needed.
        /// </summary>
        /// <param name="vw">The Vowpal Wabbit instance to use.</param>
        /// <param name="context">A user-defined context for the decision.</param>
        /// <returns>A decision tuple containing the index of the action to take (1-based), and the Id of the model or policy used to make the decision.
        /// Can be null if the Policy is not ready yet (e.g. model not loaded).</returns>
        protected override PolicyDecision<int[]> MapContext(VowpalWabbit<TContext, TActionDependentFeature> vw, TContext context)
        {
            if (this.developmentMode)
            {
                Trace.TraceInformation("Example Context: {0}", VowpalWabbitMultiLine.SerializeToString(vw, context, this.getContextFeaturesFunc(context)));
            }

            IReadOnlyCollection<TActionDependentFeature> features = this.getContextFeaturesFunc(context);

            // return indices
            ActionDependentFeature<TActionDependentFeature>[] vwMultilabelPredictions = vw.Predict(context, features);

            // VW multi-label predictions are 0-based
            var actions = vwMultilabelPredictions.Select(p => p.Index + 1).ToArray();
            var state = new VWState { ModelId = vw.Native.ID };

            return PolicyDecision.Create(actions, state);
        }

        /// <summary>
        /// Returns the number of actions defined by this context.
        /// </summary>
        public int GetNumberOfActions(TContext context)
        {
            var adfs = this.getContextFeaturesFunc(context);
            return adfs == null ? 0 : adfs.Count;
        }
    }
}
