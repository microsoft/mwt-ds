using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal class DecisionServicePolicy<TContext, TAction> 
        : AbstractModelListener, IContextMapper<TContext, TAction>
    {
        private IContextMapper<TContext, TAction> contextMapper;
        private IUpdatable<Stream> updatable;
        private readonly TimeSpan modelBlobPollDelay;
        private readonly string updateModelTaskId = "model";

        internal DecisionServicePolicy(
            IContextMapper<TContext, TAction> contextMapper,
            DecisionServiceConfiguration config,
            ApplicationTransferMetadata metaData)
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

        internal override void Receive(object sender, Stream model)
        {
            if (this.updatable != null)
            {
                this.updatable.Update(model);
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

        public INumberOfActionsProvider<TContext> NumActionsProvider 
        { 
            get
            { 
                return this.contextMapper as INumberOfActionsProvider<TContext>; 
            }
        }

        public PolicyDecision<TAction> MapContext(TContext context)
        {
            return this.contextMapper.MapContext(context);
        }

        internal override void DisposeInternal()
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
