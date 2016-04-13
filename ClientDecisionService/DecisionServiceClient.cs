using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class DecisionServiceClientBase<TContext, TAction, TPolicyValue, TClientType> : IDisposable
    {
        internal DecisionServiceClientInternal<TContext, TAction, TPolicyValue> client;

        internal DecisionServiceClientBase(DecisionServiceClientInternal<TContext, TAction, TPolicyValue> client)
        {
            this.client = client;
        }

        public Task DownloadModelAndUpdate(CancellationToken cancellationToken)
        {
            return this.client.DownloadModelAndUpdate(cancellationToken);
        }

        /// <summary>
        /// Report a simple float reward for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="reward">The simple float reward.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        public void ReportReward(float reward, UniqueEventID uniqueKey)
        {
            this.client.ReportReward(uniqueKey, reward);
        }

        /// <summary>
        /// Report an outcome in JSON format for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="outcomeJson">The outcome object in JSON format.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        /// <remarks>
        /// Outcomes are general forms of observations that can be converted to simple float rewards as required by some ML algorithms for optimization.
        /// </remarks>
        public void ReportOutcome(object outcome, UniqueEventID uniqueKey)
        {
            this.client.ReportOutcome(outcome, uniqueKey);
        }

        public void UpdateModel(Stream model)
        {
            this.client.UpdateModel(model);
        }

        public void Flush()
        {
            this.client.Flush();
        }

        public void Dispose()
        {
            if (this.client != null)
            {
                this.client.Dispose();
                this.client = null;
            }
        }
    }

    public sealed class DecisionServiceClientTypes
    {
        public interface Default
        { }

        public interface WithDefaultAction
        { }
    }

    public sealed class DecisionServiceClient<TContext, TAction, TPolicyValue>
        : DecisionServiceClientBase<TContext, TAction, TPolicyValue, DecisionServiceClientTypes.Default>
    {
        internal DecisionServiceClient(DecisionServiceClientInternal<TContext, TAction, TPolicyValue> client)
            : base(client)
        {
        }

        public TAction ChooseAction(UniqueEventID uniqueKey, TContext context)
        {
            return this.client.ChooseAction(uniqueKey, context);
        }
    }

    public sealed class DecisionServiceClientWithDefaultAction<TContext, TAction, TPolicyValue> 
        : DecisionServiceClientBase<TContext, TAction, TPolicyValue, DecisionServiceClientTypes.WithDefaultAction>
    {
        internal DecisionServiceClientWithDefaultAction(DecisionServiceClientInternal<TContext, TAction, TPolicyValue> client)
            : base(client)
        {
        }

        public TAction ChooseAction(UniqueEventID uniqueKey, TContext context, TAction defaultAction)
        {
            return this.client.ChooseAction(uniqueKey, context, defaultAction);
        }
    }
}
