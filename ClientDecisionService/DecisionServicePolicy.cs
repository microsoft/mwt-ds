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
    public class UnboundContextMapper<TContext, TMapperValue>
    {
        internal IContextMapper<TContext, TMapperValue> DefaultPolicy { get; set; }

        internal DecisionServiceConfiguration Configuration { get; set; }

        internal ApplicationTransferMetadata Metadata { get; set; }
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

    public class UnboundExplorer<TContext, TValue, TExplorerState, TMapperValue>
    {
        public IExplorer<TContext, TValue, TExplorerState, TMapperValue> Explorer { get; set; }

        public UnboundContextMapper<TContext, TMapperValue> ContextMapper { get; set; }
    }

    public class UnboundExplorer
    {
        public static UnboundExplorer<TContext, TValue, TExplorerState, TMapperValue> Create<TContext, TValue, TExplorerState, TMapperValue>(
            UnboundContextMapper<TContext, TMapperValue> unboundContextMapper,
            IExplorer<TContext, TValue, TExplorerState, TMapperValue> explorer)
        {
            return new UnboundExplorer<TContext, TValue, TExplorerState, TMapperValue> { Explorer = explorer, ContextMapper = unboundContextMapper };
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
        where TExplorer :
            IExplorer<TContext, uint, TExplorerState, uint>
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

    internal static class VWPolicy
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

        public static UnboundContextMapper<TContext, TValue> Wrap<TContext, TValue>
            (IContextMapper<TContext, TValue> contextMapper, DecisionServiceConfiguration config)
        {
            var metaData = GetBlobLocations(config);

            // conditionally wrap if it can be updated.
            var updatableContextMapper = contextMapper as IUpdatable<Stream>;

            IContextMapper<TContext, TValue> policy;

            if (config.OfflineMode || metaData == null || updatableContextMapper == null)
                policy = contextMapper;
            else
                policy = new DecisionServicePolicy<TContext, TValue>(contextMapper, config, metaData);

            return new UnboundContextMapper<TContext, TValue> { DefaultPolicy = policy, Configuration = config, Metadata = metaData };
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
