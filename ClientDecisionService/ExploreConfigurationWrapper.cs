using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class ExploreConfigurationWrapper<TContext, TAction, TPolicyValue> : AbstractModelListener, IModelSender
    {
        internal event EventHandler<Stream> sendModelHandler;

        event EventHandler<Stream> IModelSender.Send
        {
            add { this.sendModelHandler += value; }
            remove { this.sendModelHandler -= value; }
        }

        internal IExplorer<TAction, TPolicyValue> Explorer { get; set; }

        internal IFullExplorer<TContext, TAction> InitialFullExplorer { get; set; }

        internal DecisionServiceConfigurationWrapper<TContext, TPolicyValue> ContextMapper { get; set; }

        internal override void Receive(object sender, Stream model)
        {
            if (sendModelHandler != null)
            {
                sendModelHandler(sender, model);
            }
        }

        public DecisionServiceClient<TContext, TAction, TPolicyValue> ExploitUntilModel(IContextMapper<TContext, TPolicyValue> initialPolicy, IRecorder<TContext, TAction> recorder = null)
        {
            this.ContextMapper.InitialPolicy = initialPolicy;
            return DecisionServiceClient.Create(this, recorder);
        }

        public DecisionServiceClient<TContext, TAction, TPolicyValue> ExploreUntilModel(IFullExplorer<TContext, TAction> initialExplorer, IRecorder<TContext, TAction> recorder = null)
        {
            this.InitialFullExplorer = initialExplorer;
            return DecisionServiceClient.Create(this, recorder);
        }
    }

    public class ExploreConfigurationWrapper
    {
        public static ExploreConfigurationWrapper<TContext, TAction, TPolicyValue>
        Create<TContext, TAction, TPolicyValue>(
            DecisionServiceConfigurationWrapper<TContext, TPolicyValue> unboundContextMapper,
            IExplorer<TAction, TPolicyValue> explorer)
        {
            var unboundExplorer = new ExploreConfigurationWrapper<TContext, TAction, TPolicyValue> { Explorer = explorer, ContextMapper = unboundContextMapper };
            unboundContextMapper.Subscribe(unboundExplorer);
            return unboundExplorer;
        }
    }
}
