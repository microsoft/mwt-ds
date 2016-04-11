using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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

        public DecisionServiceClientAction<TContext, TAction, TPolicyValue> ExploitUntilModel(IRecorder<TContext, TAction> recorder = null)
        {
            return DecisionService.CreateActionMode(this, recorder);
        }

        public DecisionServiceClient<TContext, TAction, TPolicyValue> ExploitUntilModel(IContextMapper<TContext, TPolicyValue> initialPolicy, IRecorder<TContext, TAction> recorder = null)
        {
            this.ContextMapper.InitialPolicy = initialPolicy;
            return DecisionService.CreatePolicyMode(this, recorder);
        }

        public DecisionServiceClient<TContext, TAction, TPolicyValue> ExploreUntilModel(IFullExplorer<TContext, TAction> initialExplorer, IRecorder<TContext, TAction> recorder = null)
        {
            this.InitialFullExplorer = initialExplorer;
            return DecisionService.CreatePolicyMode(this, recorder);
        }

        // TODO: add overload for DefaultAction mode?
        public async Task<DecisionServiceClient<TContext, TAction, TPolicyValue>> LoadModelAsync(CancellationTokenSource cancelToken)
        {
            var client = DecisionService.CreatePolicyMode(this);

            var modelMetadata = new AzureBlobUpdateMetadata(
                "model", this.ContextMapper.Metadata.ModelBlobUri,
                this.ContextMapper.Metadata.ConnectionString,
                this.ContextMapper.Configuration.BlobOutputDir, TimeSpan.MinValue,
                modelFile => 
                {
                    using (var modelStream = File.OpenRead(modelFile))
                    {
                        client.UpdateModel(modelStream);
                        Trace.TraceInformation("Model download succeeded.");
                    }
                },
                null,
                cancelToken);

            await AzureBlobUpdateTask.DownloadAsync(modelMetadata);

            return client;
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
