using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal static class DecisionServicePolicy
    {
        public static IContextMapper<TContext, TValue> Wrap<TContext, TValue>
            (IContextMapper<TContext, TValue> contextMapper, DecisionServiceConfiguration config, ApplicationTransferMetadata metaData)
        {
            // conditionally wrap if it can be updated.
            var updatableContextMapper = contextMapper as IUpdatable<Stream>;

            if (config.OfflineMode || metaData == null || updatableContextMapper == null)
                return contextMapper;

            return new DecisionServicePolicy<TContext, TValue>(contextMapper, config, metaData);
        }
    }

    internal class DecisionServicePolicy<TContext, TValue> 
        : IDisposable, IContextMapper<TContext, TValue>
    {
        private IContextMapper<TContext, TValue> contextMapper;
        private IUpdatable<Stream> updatable;
        private readonly TimeSpan modelBlobPollDelay;
        private readonly string updateModelTaskId = "model";

        internal DecisionServicePolicy(IContextMapper<TContext, TValue> contextMapper, DecisionServiceConfiguration config, ApplicationTransferMetadata metaData)
        {
            this.contextMapper = contextMapper;
            this.updatable = contextMapper as IUpdatable<Stream>;
            if (this.updatable == null)
                throw new ArgumentException("contextMapper must be of type IUpdatable<Stream>");

            this.modelBlobPollDelay = config.PollingForModelPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForModelPeriod;

            if (this.modelBlobPollDelay != TimeSpan.MinValue)
            {
                AzureBlobUpdater.RegisterTask(
                    this.updateModelTaskId,
                    metaData.ModelBlobUri,
                    metaData.ConnectionString,
                    config.BlobOutputDir, 
                    this.modelBlobPollDelay,
                    this.UpdateContextMapperFromFile,
                    config.ModelPollFailureCallback);
            }
        }
        private void UpdateContextMapperFromFile(string modelFile)
        {
            using (var stream = File.OpenRead(modelFile))
            {
                this.updatable.Update(stream);

                Trace.TraceInformation("Model update succeeded.");
            }
        }

        public Decision<TValue> MapContext(TContext context, ref uint numActionsVariable)
        {
            return this.contextMapper.MapContext(context, ref numActionsVariable);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                var disposable = this.contextMapper as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                    this.contextMapper = null;
                }
            }
        }
    }
}
