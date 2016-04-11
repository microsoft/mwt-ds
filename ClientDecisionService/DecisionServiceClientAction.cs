using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceClientAction<TContext, TAction, TPolicyValue> : DecisionServiceBaseClient<TContext, TAction, TPolicyValue>
    {
        public DecisionServiceClientAction(
            DecisionServiceConfiguration config,
            ApplicationTransferMetadata metaData,
            IExplorer<TAction, TPolicyValue> explorer,
            IContextMapper<TContext, TPolicyValue> internalPolicy,
            IContextMapper<TContext, TPolicyValue> initialPolicy = null,
            IFullExplorer<TContext, TAction> initialExplorer = null,
            IRecorder<TContext, TAction> recorder = null)
            : base(config, metaData, explorer, internalPolicy, initialPolicy, initialExplorer, recorder) { }

        public TAction ChooseAction(UniqueEventID uniqueKey, TContext context, TAction initialAction)
        {
            return this.mwtExplorer.ChooseAction(uniqueKey, context, initialAction);
        }
    }
}
