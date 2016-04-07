using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class ExploreConfigurationWrapper<TContext, TValue, TExplorerState, TMapperValue> : AbstractModelListener, IModelSender
    {
        internal event EventHandler<Stream> sendModelHandler;

        event EventHandler<Stream> IModelSender.Send
        {
            add { this.sendModelHandler += value; }
            remove { this.sendModelHandler -= value; }
        }

        internal IExplorer<TContext, TValue, TExplorerState, TMapperValue> Explorer { get; set; }

        internal DecisionServiceConfigurationWrapper<TContext, TMapperValue> ContextMapper { get; set; }

        internal override void Receive(object sender, Stream model)
        {
            if (sendModelHandler != null)
            {
                sendModelHandler(sender, model);
            }
        }

        public DecisionServiceClient<TContext, TValue, TExplorerState, TMapperValue> CreateDecisionServiceClient(IRecorder<TContext, TValue, TExplorerState> recorder = null)
        {
            return DecisionServiceClient.Create(this, recorder);
        }
    }

    public class ExploreConfigurationWrapper
    {
        public static ExploreConfigurationWrapper<TContext, TValue, TExplorerState, TMapperValue>
            Create<TContext, TValue, TExplorerState, TMapperValue>(
                DecisionServiceConfigurationWrapper<TContext, TMapperValue> unboundContextMapper,
                IExplorer<TContext, TValue, TExplorerState, TMapperValue> explorer)
        {
            var unboundExplorer = new ExploreConfigurationWrapper<TContext, TValue, TExplorerState, TMapperValue> { Explorer = explorer, ContextMapper = unboundContextMapper };
            unboundContextMapper.Subscribe(unboundExplorer);
            return unboundExplorer;
        }
    }
}
