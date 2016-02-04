namespace ClientDecisionService.SingleAction
{
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
        public VWJsonPolicy(Action<string, string> setModelIdCallback, string vwModelFile = null)
            : base(setModelIdCallback, vwModelFile)
        { }

        public VWJsonPolicy(Action<string, string> setModelIdCallback, Stream vwModelStream)
            : base(setModelIdCallback, vwModelStream)
        { }

        /// <summary>
        /// Scores the model against the specified context and returns the chosen action (1-based index).
        /// </summary>
        /// <param name="contextJson">The context object in serialized JSON format.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>An unsigned integer representing the chosen action.</returns>
        public override uint ChooseAction(string contextJson, uint numActionsVariable = uint.MaxValue)
        {
            if (vwPool == null)
            {
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
            }
            using (var vw = vwPool.GetOrCreate())
            {
                this.setModelIdCallback(contextJson, vw.Value.Native.ID);

                var vwJson = new VowpalWabbitJsonSerializer(vw.Value.Native);
                vwJson.Parse(contextJson);
                VowpalWabbitExample vwExample = vwJson.CreateExample();

                return (uint)vw.Value.Native.Predict(vwExample, VowpalWabbitPredictionType.CostSensitive);
            }
        }
    }
}

namespace ClientDecisionService.MultiAction
{
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
            : base(null, setModelIdCallback, vwModelFile)
        { }

        public VWJsonPolicy(Action<string, string> setModelIdCallback, Stream vwModelStream)
            : base(null, setModelIdCallback, vwModelStream)
        { }

        /// <summary>
        /// Scores the model against the specified context and returns a list of actions (1-based index).
        /// </summary>
        /// <param name="contextJson">The context object in serialized JSON format.</param>
        /// <param name="numActionsVariable">Optional; Number of actions available which may be variable across decisions.</param>
        /// <returns>List of predicted actions.</returns>
        public override uint[] ChooseAction(string contextJson, uint numActionsVariable = uint.MaxValue)
        {
            if (vwPool == null)
            {
                throw new InvalidOperationException("A VW model must be supplied before the call to ChooseAction.");
            }
            using (var vw = vwPool.GetOrCreate())
            {
                // Callback to store model Id in the Context
                this.setModelIdCallback(contextJson, vw.Value.Native.ID);

                var vwJson = new VowpalWabbitJsonSerializer(vw.Value.Native);
                vwJson.Parse(contextJson);
                VowpalWabbitExample vwExample = vwJson.CreateExample();

                int[] vwMultilabelPredictions = vw.Value.Native.Predict(vwExample, VowpalWabbitPredictionType.Multilabel);

                // VW multi-label predictions are 0-based
                return vwMultilabelPredictions.Select(a => (uint)(a + 1)).ToArray();
            }
        }
    }
}