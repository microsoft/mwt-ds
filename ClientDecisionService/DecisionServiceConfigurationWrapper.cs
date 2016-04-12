using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceConfigurationWrapper<TContext, TPolicyValue> : AbstractModelListener, IModelSender
    {
        private EventHandler<Stream> sendModelHandler;

        event EventHandler<Stream> IModelSender.Send
        {
            add { this.sendModelHandler += value; }
            remove { this.sendModelHandler -= value; }
        }

        /// <summary>
        /// The policy used internally to handle ML models (for example, VWPolicy or DecisionServicePolicy).
        /// </summary>
        internal IContextMapper<TContext, TPolicyValue> InternalPolicy { get; set; }

        internal IContextMapper<TContext, TPolicyValue> InitialPolicy { get; set; }

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
        public static ExploreConfigurationWrapper<TContext, int, int>
            WithEpsilonGreedy<TContext>(
                this DecisionServiceConfigurationWrapper<TContext, int> mapper,
                float epsilon,
                int numActionsVariable)
        {
            return ExploreConfigurationWrapper.Create(mapper, new EpsilonGreedyExplorer(epsilon, numActionsVariable));
        }

        public static ExploreConfigurationWrapper<TContext, int[], int[]>
            WithTopSlotEpsilonGreedy<TContext>(
                this DecisionServiceConfigurationWrapper<TContext, int[]> mapper,
                float epsilon)
        {
            return ExploreConfigurationWrapper.Create(mapper, new TopSlotExplorer(new EpsilonGreedyExplorer(epsilon)));
        }
    }
}
