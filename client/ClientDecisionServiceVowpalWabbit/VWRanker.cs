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
    public class VWRanker<TContext> :
        VWBaseContextMapper<VowpalWabbitThreadedPrediction<TContext>, VowpalWabbit<TContext>, TContext, int[]>,
        IRanker<TContext>, INumberOfActionsProvider<TContext>
    {
        private readonly IVowpalWabbitMultiExampleSerializerCompiler<TContext> serializer;

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
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

        protected override PolicyDecision<int[]> MapContext(VowpalWabbit<TContext> vw, TContext context)
        {
            if (this.developmentMode)
            {
                using (var serializer = vw.Serializer.Create(vw.Native))
                {
                    Trace.TraceInformation("Example Context: {0}", serializer.SerializeToString(context));
                }
            }

            ActionScore[] vwMultilabelPredictions = vw.Predict(context, VowpalWabbitPredictionType.ActionScore);

            // VW multi-label predictions are 0-based
            var actions = vwMultilabelPredictions.Select(a => (int)a.Action + 1).ToArray();
            var state = new VWState { ModelId = vw.Native.ID };

            return PolicyDecision.Create(actions, state);
        }

        public int GetNumberOfActions(TContext context)
        {
            return this.serializer.GetNumberOfActionDependentExamples(context);
        }
    }

    public class VWRanker<TContext, TActionDependentFeature> :
        VWBaseContextMapper<VowpalWabbitThreadedPrediction<TContext, TActionDependentFeature>, VowpalWabbit<TContext, TActionDependentFeature>, TContext, int[]>,
        IRanker<TContext>, INumberOfActionsProvider<TContext>
    {
        private readonly Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc;

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        public VWRanker(
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            Stream vwModelStream = null,
            ITypeInspector typeInspector = null,
            bool developmentMode = false)
            : base(vwModelStream, typeInspector, developmentMode)
        {
            this.getContextFeaturesFunc = getContextFeaturesFunc;
        }

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

        public int GetNumberOfActions(TContext context)
        {
            var adfs = this.getContextFeaturesFunc(context);
            return adfs == null ? 0 : adfs.Count;
        }
    }
}
