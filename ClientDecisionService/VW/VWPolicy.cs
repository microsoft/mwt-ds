using Microsoft.Research.MultiWorldTesting.ClientLibrary.VW;
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
    internal class VWPolicy<TContext> 
        : VWBaseContextMapper<VowpalWabbitThreadedPrediction<TContext>, VowpalWabbit<TContext>, TContext, uint>, IPolicy<TContext>
    {
        /// <summary>
        /// Constructor using a memory stream.
        /// </summary>
        /// <param name="vwModelStream">The VW model memory stream.</param>
        internal VWPolicy(Stream vwModelStream = null, VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json)
            : base(vwModelStream, featureDiscovery)
        {
        }

        protected override Decision<uint> MapContext(VowpalWabbit<TContext> vw, TContext context)
        {
            var action = (uint)vw.Predict(context, VowpalWabbitPredictionType.CostSensitive);
            var state = new VWState { ModelId = vw.Native.ID };

            return Decision.Create(action, state);
        }
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
}