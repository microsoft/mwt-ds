using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class ExploreConfigurationWrapper<TContext, TValue, TMapperValue> : AbstractModelListener, IModelSender
    {
        internal event EventHandler<Stream> sendModelHandler;

        event EventHandler<Stream> IModelSender.Send
        {
            add { this.sendModelHandler += value; }
            remove { this.sendModelHandler -= value; }
        }

        internal IExplorer<TValue, TMapperValue> Explorer { get; set; }

        internal DecisionServiceConfigurationWrapper<TContext, TMapperValue> ContextMapper { get; set; }

        internal override void Receive(object sender, Stream model)
        {
            if (sendModelHandler != null)
            {
                sendModelHandler(sender, model);
            }
        }

        public DecisionServiceClient<TContext, TValue, TMapperValue> CreateDecisionServiceClient(IRecorder<TContext, TValue> recorder = null)
        {
            return DecisionServiceClient.Create(this, recorder);
        }
    }

    public class ExploreConfigurationWrapper
    {
        public static ExploreConfigurationWrapper<TContext, TValue, TMapperValue>
            Create<TContext, TValue, TMapperValue>(
                DecisionServiceConfigurationWrapper<TContext, TMapperValue> unboundContextMapper,
                IExplorer<TValue, TMapperValue> explorer)
        {
            var unboundExplorer = new ExploreConfigurationWrapper<TContext, TValue, TMapperValue> { Explorer = explorer, ContextMapper = unboundContextMapper };
            unboundContextMapper.Subscribe(unboundExplorer);
            return unboundExplorer;
        }
    }
}
