using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal interface IModelSender
    {
        event EventHandler<Stream> Send;
    }

    public class UnboundContextMapper<TContext, TMapperValue> : AbstractModelListener, IModelSender
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

    public static class ExplorerExtensions
    {
        public static UnboundExplorer<TContext, uint, EpsilonGreedyState, uint> WithEpsilonGreedy<TContext>(
            this UnboundContextMapper<TContext, uint> mapper, float epsilon, uint numActionsVariable = uint.MaxValue)
        {
            return UnboundExplorer.Create(mapper, new EpsilonGreedyExplorer<TContext>(mapper.DefaultPolicy, epsilon, numActionsVariable));
        }

        public static UnboundExplorer<TContext, uint[], EpsilonGreedyState, uint[]> WithTopSlotEpsilonGreedy<TContext>(
            this UnboundContextMapper<TContext, uint[]> mapper, float epsilon, uint numActionsVariable = uint.MaxValue)
        {
            var explorer = Explorer.Create<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>(
                mapper.DefaultPolicy,
                policy => new EpsilonGreedyExplorer<TContext>(policy, epsilon, numActionsVariable),
                numActionsVariable);

            return UnboundExplorer.Create(mapper, explorer);
        }
    }

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

    public static class Explorer
    {
        public static UnboundExplorer<TContext, uint, EpsilonGreedyState, uint>
            WithEpsilonGreedy<TContext>(UnboundContextMapper<TContext, uint> mapper, float epsilon, uint numActionsVariable = uint.MaxValue)
        {
            return UnboundExplorer.Create(mapper, new EpsilonGreedyExplorer<TContext>(mapper.DefaultPolicy, epsilon, numActionsVariable));
        }

        public static UnboundExplorer<TContext, uint[], EpsilonGreedyState, uint[]>
            CreateTopSlotEpsilonGreedy<TContext>(UnboundContextMapper<TContext, uint[]> mapper, float epsilon, uint numActionsVariable = uint.MaxValue)
        {
            var explorer = Create<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>(
                mapper.DefaultPolicy,
                policy => new EpsilonGreedyExplorer<TContext>(policy, epsilon, numActionsVariable),
                numActionsVariable);

            return UnboundExplorer.Create(mapper, explorer);
        }

        internal static TopSlotExplorer<TContext, TExplorer, TExplorerState> Create<TContext, TExplorer, TExplorerState>(
            IContextMapper<TContext, uint[]> defaultRanker,
            Func<IContextMapper<TContext, uint>, TExplorer> singleExplorerFactory,
            uint numActions)
        where TExplorer : IVariableActionExplorer<TContext, uint, TExplorerState, uint>
        {
            return new TopSlotExplorer<TContext, TExplorer, TExplorerState>(defaultRanker, singleExplorerFactory, numActions);
        }

        // TODO: Add back
        //internal static TopSlotExplorer<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>
        //    CreateTopSlotEpsilonGreedyExplorer<TContext>(
        //        IContextMapper<TContext, uint[]> defaultPolicy,
        //        float epsilon,
        //        uint numActionsVariable = uint.MaxValue)
        //{
        //    return Create<TContext, EpsilonGreedyExplorer<TContext>, EpsilonGreedyState>(
        //        defaultPolicy,
        //        policy => new EpsilonGreedyExplorer<TContext>(policy, epsilon, numActionsVariable),
        //        numActionsVariable);
        //}

        //internal static TopSlotExplorer<TContext, TauFirstExplorer<TContext>, TauFirstState>
        //    CreateTopSlotTauFirstExplorer<TContext>(
        //        IContextMapper<TContext, uint[]> defaultPolicy,
        //        uint tau,
        //        uint numActionsVariable = uint.MaxValue)
        //{
        //    return Explorer<TContext>.CreateTopSlotTauFirstExplorer(defaultPolicy, tau, numActionsVariable);
        //}

        //internal static EpsilonGreedyExplorer<TContext> CreateEpsilonGreedyExplorer<TContext>(IContextMapper<TContext, uint> defaultPolicy, float epsilon, uint numActionsVariable = uint.MaxValue)
        //{
        //    return new EpsilonGreedyExplorer<TContext>(defaultPolicy, epsilon, numActionsVariable);
        //}

        //internal static TauFirstExplorer<TContext> CreateTauFirstExplorer<TContext>(IContextMapper<TContext, uint> defaultPolicy, uint tau, uint numActionsVariable = uint.MaxValue)
        //{
        //    return new TauFirstExplorer<TContext>(defaultPolicy, tau, numActionsVariable);
        //}

        //internal static SoftmaxExplorer<TContext> CreateSoftmaxExplorer<TContext>(IContextMapper<TContext, float[]> defaultScorer, float lambda, uint numActionsVariable = uint.MaxValue)
        //{
        //    return new SoftmaxExplorer<TContext>(defaultScorer, lambda, numActionsVariable);
        //}

        // TODO: add more factory methods
    }

    public static class VWPolicy
    {
        public static UnboundContextMapper<string, uint> CreateJsonPolicy(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = true;
            return VWPolicy.Wrap(new VWJsonPolicy(config.ModelStream), config);
        }

        public static UnboundContextMapper<string, uint[]> CreateJsonRanker(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = true;
            return VWPolicy.Wrap(new VWJsonRanker(config.ModelStream), config);
        }

        public static UnboundContextMapper<TContext, uint> CreatePolicy<TContext>(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = false;
            return VWPolicy.Wrap(new VWPolicy<TContext>(config.ModelStream, config.FeatureDiscovery), config);
        }

        public static UnboundContextMapper<TContext, uint[]> CreateRanker<TContext>(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = false;
            return VWPolicy.Wrap(new VWRanker<TContext>(config.ModelStream, config.FeatureDiscovery), config);
        }

        public static UnboundContextMapper<TContext, uint[]> CreateRanker<TContext, TActionDependentFeature>(
            DecisionServiceConfiguration config,
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc)
        {
            config.UseJsonContext = false;
            return VWPolicy.Wrap(new VWRanker<TContext, TActionDependentFeature>(getContextFeaturesFunc, config.ModelStream, config.FeatureDiscovery), config);
        }

        public static UnboundContextMapper<string, uint> StartWithJsonPolicy(
            DecisionServiceConfiguration config,
            IContextMapper<string, uint> initialPolicy)
        {
            config.UseJsonContext = true;
            return VWPolicy.Wrap(MultiPolicy.Create(new VWJsonPolicy(config.ModelStream), initialPolicy), config);
        }

        public static UnboundContextMapper<string, uint[]> StartWithJsonRanker(
            DecisionServiceConfiguration config,
            IContextMapper<string, uint[]> initialPolicy)
        {
            config.UseJsonContext = true;
            return VWPolicy.Wrap(MultiPolicy.Create(new VWJsonRanker(config.ModelStream), initialPolicy), config);
        }

        public static UnboundContextMapper<TContext, uint> StartWithPolicy<TContext>(
            DecisionServiceConfiguration config,
            IContextMapper<TContext, uint> initialPolicy)
        {
            config.UseJsonContext = false;
            return VWPolicy.Wrap(MultiPolicy.Create(
                new VWPolicy<TContext>(config.ModelStream, config.FeatureDiscovery), initialPolicy),
                config);
        }

        public static UnboundContextMapper<TContext, uint[]> StartWithRanker<TContext>(
            DecisionServiceConfiguration config,
            IContextMapper<TContext, uint[]> initialPolicy)
        {
            config.UseJsonContext = false;
            return VWPolicy.Wrap(MultiPolicy.Create(
                new VWRanker<TContext>(config.ModelStream, config.FeatureDiscovery), initialPolicy),
                config);
        }

        public static UnboundContextMapper<TContext, uint[]> StartWithRanker<TContext, TActionDependentFeature>(
            DecisionServiceConfiguration config,
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            IContextMapper<TContext, uint[]> initialPolicy)
        {
            config.UseJsonContext = false;
            return VWPolicy.Wrap(MultiPolicy.Create(
                new VWRanker<TContext, TActionDependentFeature>(getContextFeaturesFunc, config.ModelStream, config.FeatureDiscovery), initialPolicy),
                config);
        }


        public static UnboundContextMapper<TContext, TValue> Wrap<TContext, TValue>
            (IContextMapper<TContext, TValue> vwPolicy, DecisionServiceConfiguration config)
        {
            var metaData = GetBlobLocations(config);
            var ucm = new UnboundContextMapper<TContext, TValue> { Configuration = config, Metadata = metaData };

            // conditionally wrap if it can be updated.
            var updatableContextMapper = vwPolicy as IUpdatable<Stream>;

            IContextMapper<TContext, TValue> policy;

            if (config.OfflineMode || metaData == null || updatableContextMapper == null)
                policy = vwPolicy;
            else
            {
                var dsPolicy = new DecisionServicePolicy<TContext, TValue>(vwPolicy, config, metaData);
                dsPolicy.Subscribe(ucm);
                policy = dsPolicy;
            }
            ucm.DefaultPolicy = policy;
            
            return ucm;
        }

        internal static ApplicationTransferMetadata GetBlobLocations(DecisionServiceConfiguration config)
        {
            if (config.OfflineMode)
                return null;

            string redirectionBlobLocation = string.Format(DecisionServiceConstants.RedirectionBlobLocation, config.AuthorizationToken);

            try
            {
                using (var wc = new WebClient())
                {
                    string jsonMetadata = wc.DownloadString(redirectionBlobLocation);
                    return JsonConvert.DeserializeObject<ApplicationTransferMetadata>(jsonMetadata);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("Unable to retrieve blob locations from storage using the specified token", ex);
            }
        }
    }

    internal class DecisionServicePolicy<TContext, TValue> 
        : AbstractModelListener, IContextMapper<TContext, TValue>
    {
        private IContextMapper<TContext, TValue> contextMapper;
        private IUpdatable<Stream> updatable;
        private readonly TimeSpan modelBlobPollDelay;
        private readonly string updateModelTaskId = "model";

        internal DecisionServicePolicy(
            IContextMapper<TContext, TValue> contextMapper,
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

        public Decision<TValue> MapContext(TContext context)
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
