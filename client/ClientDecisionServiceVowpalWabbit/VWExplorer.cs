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
    public sealed class VWExplorer<TContext> :
        VWBaseContextMapper<VowpalWabbit<TContext>, TContext, ActionProbability[]>,
        IContextMapper<TContext, ActionProbability[]>, INumberOfActionsProvider<TContext>
    {
        private readonly IVowpalWabbitSerializerCompiler<TContext> serializer;
        private readonly IVowpalWabbitMultiExampleSerializerCompiler<TContext> multiSerializer;

        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
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
        
        protected override VowpalWabbitThreadedPredictionBase<VowpalWabbit<TContext>> CreatePool(VowpalWabbitSettings settings)
        {
            return new VowpalWabbitThreadedPrediction<TContext>(settings);
        }

        protected override PolicyDecision<ActionProbability[]> MapContext(VowpalWabbit<TContext> vw, TContext context)
        {
            if (this.developmentMode)
            {
                using (var serializer = vw.Serializer.Create(vw.Native))
                {
                    Trace.TraceInformation("Example Context: {0}", serializer.SerializeToString(context));
                }
            }

            var vwPredictions = vw.Predict(context, VowpalWabbitPredictionType.ActionScore);

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

        public int GetNumberOfActions(TContext context)
        {
            if (this.multiSerializer == null)
                throw new NotSupportedException("The serializer for " + typeof(TContext) + " is not a multi example serializer");

            return this.multiSerializer.GetNumberOfActionDependentExamples(context);
        }
    }
}
