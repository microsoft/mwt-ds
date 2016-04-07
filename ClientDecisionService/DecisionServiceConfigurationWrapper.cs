using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceConfigurationWrapper<TContext, TMapperValue> : AbstractModelListener, IModelSender
    {
        private EventHandler<Stream> sendModelHandler;

        event EventHandler<Stream> IModelSender.Send
        {
            add { this.sendModelHandler += value; }
            remove { this.sendModelHandler -= value; }
        }

        internal IContextMapper<TContext, TMapperValue> DefaultPolicy { get; set; }

        internal DecisionServiceConfiguration Configuration { get; set; }

        internal ApplicationTransferMetadata Metadata { get; set; }

        internal override void Receive(object sender, Stream model)
        {
            if (sendModelHandler != null)
            {
                sendModelHandler(sender, model);
            }
        }
    }

    public static class DecisionServiceConfigurationWrapperExtensions
    {
        public static ExploreConfigurationWrapper<TContext, uint, EpsilonGreedyState, uint>
            WithEpsilonGreedy<TContext>(
                this DecisionServiceConfigurationWrapper<TContext, uint> mapper,
                float epsilon,
                uint numActionsVariable = uint.MaxValue)
        {
            return ExploreConfigurationWrapper.Create(mapper, new EpsilonGreedyExplorer<TContext>(mapper.DefaultPolicy, epsilon, numActionsVariable));
        }

        public static ExploreConfigurationWrapper<TContext, uint[], EpsilonGreedyState, uint[]>
            WithTopSlotEpsilonGreedy<TContext>(
                this DecisionServiceConfigurationWrapper<TContext, uint[]> mapper,
                float epsilon,
                uint numActionsVariable = uint.MaxValue)
        {
            var explorer = ExplorerFactory.CreateTopSlot<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>(
                mapper.DefaultPolicy,
                policy => new EpsilonGreedyExplorer<TContext>(policy, epsilon, numActionsVariable),
                numActionsVariable);

            return ExploreConfigurationWrapper.Create(mapper, explorer);
        }
    }
}
