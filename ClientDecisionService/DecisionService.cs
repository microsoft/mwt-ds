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
            var config = new DecisionServiceConfiguration("abcd")
            {
                // options
            };
            // JSON string -> uint
            {
                var policy = VWPolicy.CreateJsonPolicy(config);
                var client = DecisionServiceClient.Create(Explorer.WithEpsilonGreedy(policy, epsilon: .3f));
            }

            {
                // TODO: rename?
                var ranker = VWPolicy.CreateRanker<MyContext>(config);
                var client = DecisionServiceClient.Create(ranker.WithTopSlotEpsilonGreedy(epsilon: .3f));
            }

            {
                var ranker = VWPolicy.CreateJsonRanker(config);
                var client = DecisionServiceClient.Create(ranker.WithTopSlotEpsilonGreedy(epsilon: .3f));
            }

            {
                var policy = VWPolicy.CreatePolicy<MyContext>(config);
                var client = DecisionServiceClient.Create(policy.WithEpsilonGreedy(epsilon: .3f));
            }

            {
                var ranker = VWPolicy.CreateRanker<MySharedContext, MyADFContext>(config, context => context.ADFs);
                var client = DecisionServiceClient.Create(ranker.WithTopSlotEpsilonGreedy(epsilon: .3f));
            }

            {
                var cachedRanker = DecisionServiceClient.AddOrGetExisting(
                    "abc",
                    token =>
                    {
                        var ranker = VWPolicy.CreateRanker<MySharedContext, MyADFContext>(config, context => context.ADFs);
                        return DecisionServiceClient.Create(ranker.WithTopSlotEpsilonGreedy(epsilon: .3f));
                    });
            }
            
        }
    }

    //public static class DecisionServiceClient
    //{
    //    public static DecisionServicePolicyClient<TContext, TExplorerState, TMapperValue>
    //        CreatePolicy<TContext, TExplorerState, TMapperValue>(UnboundExplorer<TContext, uint, TExplorerState, TMapperValue> explorer)
    //    {
    //        return new DecisionServicePolicyClient<TContext, TExplorerState, TMapperValue>(
    //            explorer.ContextMapper.Configuration, explorer.ContextMapper.Metadata, explorer.Explorer);
    //    }

    //    public static DecisionServiceRankerClient<TContext, TExplorerState, TMapperValue>
    //        CreateRanker<TContext, TExplorerState, TMapperValue>(UnboundExplorer<TContext, uint[], TExplorerState, TMapperValue> explorer)
    //    {
    //        return new DecisionServiceRankerClient<TContext, TExplorerState, TMapperValue>(
    //            explorer.ContextMapper.Configuration, explorer.ContextMapper.Metadata, explorer.Explorer);
    //    }
    //}

    // JSON direct, feature annotation
    //public static class DecisionService<TContext>
    //{
    //    public static DecisionServicePolicyClient<TContext, TExplorerState, uint> CreateEpsilonGreedy<TExplorerState>(
    //        DecisionServiceConfiguration config,
    //        Func<IContextMapper<TContext, uint>, IExplorer<TContext, uint, TExplorerState, uint>> explorerFactory,
    //        Stream vwModelStream = null,
    //        VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json,
    //        IRecorder<TContext, uint, TExplorerState> customRecorder = null)
    //    {
    //        var metaData = DecisionService.GetBlobLocations(config);
    //        var contextMapper = DecisionServicePolicy.Wrap(new VWPolicy<TContext>(vwModelStream, featureDiscovery), config, metaData);

    //        config.UseJsonContext = false;

    //        return new DecisionServicePolicyClient<TContext, TExplorerState, uint>(config, metaData, explorerFactory(contextMapper));
    //    }

    //    public static DecisionServicePolicyClient<TContext, TExplorerState, uint> CreatePolicyClient<TExplorerState>(
    //        DecisionServiceConfiguration config,
    //        Func<IContextMapper<TContext, uint>, IExplorer<TContext, uint, TExplorerState, uint>> explorerFactory,
    //        Stream vwModelStream = null,
    //        VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json,
    //        IRecorder<TContext, uint, TExplorerState> customRecorder = null)
    //    {
    //        var metaData = DecisionService.GetBlobLocations(config);
    //        var contextMapper = DecisionServicePolicy.Wrap(new VWPolicy<TContext>(vwModelStream, featureDiscovery), config, metaData);

    //        config.UseJsonContext = false;

    //        return new DecisionServicePolicyClient<TContext, TExplorerState, uint>(config, metaData, explorerFactory(contextMapper));
    //    }

    //    public static DecisionServiceRankerClient<TContext, TExplorerState, uint[]> CreateRankerClient<TExplorerState>(
    //        DecisionServiceConfiguration config,
    //        Func<IContextMapper<TContext, uint[]>, IExplorer<TContext, uint[], TExplorerState, uint[]>> explorerFactory,
    //        Stream vwModelStream = null,
    //        VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json,
    //        IRecorder<TContext, uint, TExplorerState> customRecorder = null)
    //    {
    //        var metaData = DecisionService.GetBlobLocations(config);
    //        var contextMapper = DecisionServicePolicy.Wrap(new VWRanker<TContext>(vwModelStream, featureDiscovery), config, metaData);

    //        config.UseJsonContext = false;

    //        return new DecisionServiceRankerClient<TContext, TExplorerState, uint[]>(config, metaData, explorerFactory(contextMapper));
    //    }
    //}

    //// ADF
    //public static class DecisionService<TContext, TActionDependentFeature>
    //{
    //    public static DecisionServiceRankerClient<TContext, TExplorerState, uint[]> CreateRankerClient<TExplorerState>(
    //        DecisionServiceConfiguration config,
    //        Func<IContextMapper<TContext, uint[]>, IExplorer<TContext, uint[], TExplorerState, uint[]>> explorerFactory,
    //        Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc,
    //        Stream vwModelStream = null,
    //        VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json,
    //        IRecorder<TContext, uint, TExplorerState> customRecorder = null)
    //    {
    //        var metaData = DecisionService.GetBlobLocations(config);
    //        var contextMapper = DecisionServicePolicy.Wrap(new VWRanker<TContext, TActionDependentFeature>(getContextFeaturesFunc, vwModelStream, featureDiscovery), config, metaData);
    //        config.UseJsonContext = false;

    //        return new DecisionServiceRankerClient<TContext, TExplorerState, uint[]>(config, metaData, explorerFactory(contextMapper));
    //    }
    //}

    // JSON
    //public static class DecisionService
    //{
    //    public static DecisionServicePolicyClient<string, TExplorerState, uint> CreateJsonPolicyClient<TExplorerState>(
    //        DecisionServiceConfiguration config,
    //        Func<IContextMapper<string, uint>, IExplorer<string, uint, TExplorerState, uint>> explorerFactory,
    //        IRecorder<string, uint, TExplorerState> customRecorder = null)
    //    {
    //        var metaData = GetBlobLocations(config);
    //        var contextMapper = DecisionServicePolicy.Wrap(new VWJsonPolicy(), config, metaData);

    //        config.UseJsonContext = true;

    //        return new DecisionServicePolicyClient<string, TExplorerState, uint>(config, metaData, explorerFactory(contextMapper));
    //    }

    //    public static DecisionServiceRankerClient<string, TExplorerState, uint[]> CreateJsonRankerClient<TExplorerState>(
    //        DecisionServiceConfiguration config,
    //        Func<IContextMapper<string, uint[]>, IExplorer<string, uint[], TExplorerState, uint[]>> explorerFactory,
    //        IRecorder<string, uint[], TExplorerState> customRecorder = null)
    //    {
    //        var metaData = GetBlobLocations(config);
    //        var contextMapper = DecisionServicePolicy.Wrap(new VWJsonRanker(), config, metaData);

    //        config.UseJsonContext = true;
    //        return new DecisionServiceRankerClient<string, TExplorerState, uint[]>(config, metaData, explorerFactory(contextMapper));
    //    }

    //    internal static ApplicationTransferMetadata GetBlobLocations(DecisionServiceConfiguration config)
    //    {
    //        if (config.OfflineMode)
    //            return null;

    //        string redirectionBlobLocation = string.Format(DecisionServiceConstants.RedirectionBlobLocation, config.AuthorizationToken);

    //        try
    //        {
    //            using (var wc = new WebClient())
    //            {
    //                string jsonMetadata = wc.DownloadString(redirectionBlobLocation);
    //                return JsonConvert.DeserializeObject<ApplicationTransferMetadata>(jsonMetadata);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            throw new InvalidDataException("Unable to retrieve blob locations from storage using the specified token", ex);
    //        }
    //    }
    //}
}