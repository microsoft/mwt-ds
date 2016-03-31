using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using VW;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    // TODO: remove me
    public class MyContext
    {
        public int Feature { get; set; }
    }

    public class MySharedContext
    {
        public int SharedFeature { get; set; }

        public MyADFContext[] ADFs { get; set; }
    }

    public class MyADFContext
    {
        public int Feature { get; set; }
    }

    public class Foo
    {
        public static void Run()
        {
            // JSON string -> uint
            var client = DecisionService.CreateJsonPolicyClient(
                new DecisionServiceConfiguration("abcd")
                {
                    // options
                }, 
                policy => Explorer.CreateEpsilonGreedyExplorer(policy, .3f));

            // JSON string -> uint[]
            var clientRanker = DecisionService.CreateJsonRankerClient(
                new DecisionServiceConfiguration("abc"),
                ranker => Explorer.CreateTopSlotEpsilonGreedyExplorer(ranker, .3f));

            // JSON-direct -> uint
            var clientCtxPolicy = DecisionService<MyContext>.CreatePolicyClient(
                new DecisionServiceConfiguration("abc"),
                policy => Explorer.CreateEpsilonGreedyExplorer(policy, .3f));

            // JSON-direct -> uint[]
            var clientCtxRanker = DecisionService<MyContext>.CreateRankerClient(
                new DecisionServiceConfiguration("abc"),
                ranker => Explorer.CreateTopSlotEpsilonGreedyExplorer(ranker, .3f));

            // User context + ADF type -> uint[]
            var clientAdfPolicy = DecisionService<MySharedContext, MyADFContext>.CreateRankerClient(
                new DecisionServiceConfiguration("abc"),
                ranker => Explorer.CreateTopSlotEpsilonGreedyExplorer(ranker, .3f),
                ctx => ctx.ADFs);


            /* TODO */
            var cachedRanker = DecisionServiceFactory.AddOrGetExisting(
                "abc",
                token => DecisionService<MyContext>.CreateRankerClient(
                    new DecisionServiceConfiguration(token),
                    ranker => Explorer.CreateTopSlotEpsilonGreedyExplorer(ranker, .3f)));
        }
    }

    // JSON direct, feature annotation
    public static class DecisionService<TContext>
    {
        public static DecisionServicePolicyClient<TContext, TExplorerState, uint> CreatePolicyClient<TExplorerState>(
            DecisionServiceConfiguration config,
            Func<IContextMapper<TContext, uint>, IExplorer<TContext, uint, TExplorerState, uint>> explorerFactory,
            Stream vwModelStream = null,
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json,
            IRecorder<TContext, uint, TExplorerState> customRecorder = null)
        {
            var metaData = DecisionService.GetBlobLocations(config);
            var contextMapper = DecisionServicePolicy.Wrap(new VWPolicy<TContext>(vwModelStream, featureDiscovery), config, metaData);

            config.UseJsonContext = false;

            return new DecisionServicePolicyClient<TContext, TExplorerState, uint>(config, metaData, explorerFactory(contextMapper));
        }

        public static DecisionServiceRankerClient<TContext, TExplorerState, uint[]> CreateRankerClient<TExplorerState>(
            DecisionServiceConfiguration config,
            Func<IContextMapper<TContext, uint[]>, IExplorer<TContext, uint[], TExplorerState, uint[]>> explorerFactory,
            Stream vwModelStream = null,
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json,
            IRecorder<TContext, uint, TExplorerState> customRecorder = null)
        {
            var metaData = DecisionService.GetBlobLocations(config);
            var contextMapper = DecisionServicePolicy.Wrap(new VWRanker<TContext>(vwModelStream, featureDiscovery), config, metaData);

            config.UseJsonContext = false;

            return new DecisionServiceRankerClient<TContext, TExplorerState, uint[]>(config, metaData, explorerFactory(contextMapper));
        }
    }

    // ADF
    public static class DecisionService<TContext, TActionDependentFeature>
    {
        public static DecisionServiceRankerClient<TContext, TExplorerState, uint[]> CreateRankerClient<TExplorerState>(
            DecisionServiceConfiguration config,
            Func<IContextMapper<TContext, uint[]>, IExplorer<TContext, uint[], TExplorerState, uint[]>> explorerFactory,
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
            Stream vwModelStream = null,
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json,
            IRecorder<TContext, uint, TExplorerState> customRecorder = null)
        {
            var metaData = DecisionService.GetBlobLocations(config);
            var contextMapper = DecisionServicePolicy.Wrap(new VWRanker<TContext, TActionDependentFeature>(getContextFeaturesFunc, vwModelStream, featureDiscovery), config, metaData);
            config.UseJsonContext = false;

            return new DecisionServiceRankerClient<TContext, TExplorerState, uint[]>(config, metaData, explorerFactory(contextMapper));
        }
    }

    // JSON
    public static class DecisionService
    {
        public static DecisionServicePolicyClient<string, TExplorerState, uint> CreateJsonPolicyClient<TExplorerState>(
            DecisionServiceConfiguration config,
            Func<IContextMapper<string, uint>, IExplorer<string, uint, TExplorerState, uint>> explorerFactory,
            IRecorder<string, uint, TExplorerState> customRecorder = null)
        {
            var metaData = GetBlobLocations(config);
            var contextMapper = DecisionServicePolicy.Wrap(new VWJsonPolicy(), config, metaData);

            config.UseJsonContext = true;

            return new DecisionServicePolicyClient<string, TExplorerState, uint>(config, metaData, explorerFactory(contextMapper));
        }

        public static DecisionServiceRankerClient<string, TExplorerState, uint[]> CreateJsonRankerClient<TExplorerState>(
            DecisionServiceConfiguration config,
            Func<IContextMapper<string, uint[]>, IExplorer<string, uint[], TExplorerState, uint[]>> explorerFactory,
            IRecorder<string, uint[], TExplorerState> customRecorder = null)
        {
            var metaData = GetBlobLocations(config);
            var contextMapper = DecisionServicePolicy.Wrap(new VWJsonRanker(), config, metaData);

            config.UseJsonContext = true;
            return new DecisionServiceRankerClient<string, TExplorerState, uint[]>(config, metaData, explorerFactory(contextMapper));
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
}