using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class UnboundExplorer<TContext, TValue, TExplorerState, TMapperValue> : AbstractModelListener, IModelSender
    {
        internal event EventHandler<Stream> sendModelHandler;

        event EventHandler<Stream> IModelSender.Send
        {
            add { this.sendModelHandler += value; }
            remove { this.sendModelHandler -= value; }
        }

        internal IExplorer<TContext, TValue, TExplorerState, TMapperValue> Explorer { get; set; }

        internal UnboundContextMapper<TContext, TMapperValue> ContextMapper { get; set; }

        internal override void Receive(object sender, Stream model)
        {
            if (sendModelHandler != null)
            {
                sendModelHandler(sender, model);
            }
        }
    }

    public class UnboundExplorer
    {
        public static UnboundExplorer<TContext, TValue, TExplorerState, TMapperValue> Create<TContext, TValue, TExplorerState, TMapperValue>(
            UnboundContextMapper<TContext, TMapperValue> unboundContextMapper,
            IExplorer<TContext, TValue, TExplorerState, TMapperValue> explorer)
        {
            var unboundExplorer = new UnboundExplorer<TContext, TValue, TExplorerState, TMapperValue> { Explorer = explorer, ContextMapper = unboundContextMapper };
            unboundContextMapper.Subscribe(unboundExplorer);
            return unboundExplorer;
        }
    }
}
