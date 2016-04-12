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

        internal IFullExplorer<TAction> InitialFullExplorer { get; set; }

        internal IRecorder<TContext, TAction> Recorder { get; set; }

        internal DecisionServiceConfigurationWrapper<TContext, TPolicyValue> ConfigWrapper { get; set; }

        internal override void Receive(object sender, Stream model)
        {
            if (sendModelHandler != null)
            {
                sendModelHandler(sender, model);
            }
        }

        public DecisionServiceClientWithDefaultAction<TContext, TAction, TPolicyValue> ExploitUntilModelReady()
        {
            return DecisionService.CreateActionMode(this, this.Recorder);
        }

        public DecisionServiceClient<TContext, TAction, TPolicyValue> ExploitUntilModelReady(IContextMapper<TContext, TPolicyValue> initialPolicy)
        {
            this.ConfigWrapper.InitialPolicy = initialPolicy;
            return DecisionService.CreatePolicyMode(this, this.Recorder);
        }

        public DecisionServiceClient<TContext, TAction, TPolicyValue> ExploreUntilModelReady(IFullExplorer<TAction> initialExplorer)
        {
            this.InitialFullExplorer = initialExplorer;
            return DecisionService.CreatePolicyMode(this, this.Recorder);
        }

        public ExploreConfigurationWrapper<TContext, TAction, TPolicyValue>
            WithRecorder(IRecorder<TContext, TAction> recorder)
        {
            this.Recorder = recorder;
            return this;
        }

        // TODO: add overload for DefaultAction mode?
        public async Task<DecisionServiceClient<TContext, TAction, TPolicyValue>> LoadModelAsync(CancellationTokenSource cancelToken)
        {
            var client = DecisionService.CreatePolicyMode(this);

            var modelMetadata = new AzureBlobUpdateMetadata(
                "model", this.ConfigWrapper.Metadata.ModelBlobUri,
                this.ConfigWrapper.Metadata.ConnectionString,
                this.ConfigWrapper.Configuration.BlobOutputDir, TimeSpan.MinValue,
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
            var unboundExplorer = new ExploreConfigurationWrapper<TContext, TAction, TPolicyValue> { Explorer = explorer, ConfigWrapper = unboundContextMapper };
            unboundContextMapper.Subscribe(unboundExplorer);
            return unboundExplorer;
        }
    }
}
