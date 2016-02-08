namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction
{
    using Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction;
    using System;
    using System.IO;
    using VW;
    using VW.Serializer;

    /// <summary>
    /// Represent an updatable <see cref="IPolicy<TContext>"/> object which can consume different VowpalWabbit 
    /// models to predict a list of actions from a JSON serialized context object.
    /// </summary>
    public class VWJsonPolicy : VWPolicy<string>
    {
        public VWJsonPolicy(string vwModelFile = null) : base(vwModelFile)
        { }

        public VWJsonPolicy(Stream vwModelStream) : base(vwModelStream)
        { }

        /// <summary>
        /// Scores the model against the specified context and returns the chosen action (1-based index).
        /// </summary>
        /// <param name="contextJson">The context object in serialized JSON format.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned integer representing the chosen action.</returns>
        public override PolicyDecisionTuple ChooseAction(string contextJson, uint numActionsVariable = uint.MaxValue)
        {
            if (vwPool == null)
            {
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
            }
            using (var vw = vwPool.GetOrCreate())
            {
                var vwJson = new VowpalWabbitJsonSerializer(vw.Value.Native);
                using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(contextJson))
                {
                    return new PolicyDecisionTuple
                    {
                        Action = (uint)vwExample.Predict(VowpalWabbitPredictionType.CostSensitive),
                        ModelId = vw.Value.Native.ID
                    };
                }
            }
        }
    }
}

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary.MultiAction
{
    using Microsoft.Research.MultiWorldTesting.ExploreLibrary.MultiAction;
    using System;
    using System.IO;
    using System.Linq;
    using VW;
    using VW.Serializer;

    /// <summary>
    /// Represent an updatable <see cref="IPolicy<TContext>"/> object which can consume different VowpalWabbit 
    /// models to predict a list of actions from a JSON serialized context object.
    /// </summary>
    public class VWJsonPolicy : VWPolicy<string, string>
    {
        public VWJsonPolicy(Action<string, string> setModelIdCallback, string vwModelFile = null)
            : base(null, vwModelFile)
        { }

        public VWJsonPolicy(Action<string, string> setModelIdCallback, Stream vwModelStream)
            : base(null, vwModelStream)
        { }

        /// <summary>
        /// Scores the model against the specified context and returns a list of actions (1-based index).
        /// </summary>
        /// <param name="contextJson">The context object in serialized JSON format.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>List of predicted actions.</returns>
        public override PolicyDecisionTuple ChooseAction(string contextJson, uint numActionsVariable = uint.MaxValue)
        {
            if (vwPool == null)
            {
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
            }
            using (var vw = vwPool.GetOrCreate())
            {
                var vwJson = new VowpalWabbitJsonSerializer(vw.Value.Native);
                using (VowpalWabbitExampleCollection vwExample = vwJson.ParseAndCreate(contextJson))
                {
                    int[] vwMultilabelPredictions = vwExample.Predict(VowpalWabbitPredictionType.Multilabel);

                    // VW multi-label predictions are 0-based
                    return new PolicyDecisionTuple
                    {
                        Actions = vwMultilabelPredictions.Select(a => (uint)(a + 1)).ToArray(),
                        ModelId = vw.Value.Native.ID
                    };
                }
            }
        }
    }
}