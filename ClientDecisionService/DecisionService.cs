using Microsoft.Research.MultiWorldTesting.ClientLibrary.VW;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Factory class.
    /// </summary>
    public static class DecisionService
    {
        public static DecisionServiceClient<TContext, TAction, TPolicyValue>
            CreatePolicyMode<TContext, TAction, TPolicyValue>(
                ExploreConfigurationWrapper<TContext, TAction, TPolicyValue> explorer,
                IRecorder<TContext, TAction> recorder = null)
        {
            var dsClient = new DecisionServiceClient<TContext, TAction, TPolicyValue>(
                explorer.ContextMapper.Configuration,
                explorer.ContextMapper.Metadata,
                explorer.Explorer,
                explorer.ContextMapper.InternalPolicy,
                initialExplorer: explorer.InitialFullExplorer,
                initialPolicy: explorer.ContextMapper.InitialPolicy,
                recorder: recorder);
            explorer.Subscribe(dsClient);
            return dsClient;
        }

        public static DecisionServiceClientAction<TContext, TAction, TPolicyValue>
            CreateActionMode<TContext, TAction, TPolicyValue>(
                ExploreConfigurationWrapper<TContext, TAction, TPolicyValue> explorer,
                IRecorder<TContext, TAction> recorder = null)
        {
            var dsClient = new DecisionServiceClientAction<TContext, TAction, TPolicyValue>(
                explorer.ContextMapper.Configuration,
                explorer.ContextMapper.Metadata,
                explorer.Explorer,
                explorer.ContextMapper.InternalPolicy,
                initialExplorer: explorer.InitialFullExplorer,
                initialPolicy: explorer.ContextMapper.InitialPolicy,
                recorder: recorder);
            explorer.Subscribe(dsClient);
            return dsClient;
        }

        public static DecisionServiceConfigurationWrapper<string, int> WithJsonPolicy(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = true;
            return DecisionService.Wrap(config, new VWJsonPolicy(config.ModelStream));
        }

        public static DecisionServiceConfigurationWrapper<string, int[]> WithJsonRanker(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = true;
            return DecisionService.Wrap(config, new VWJsonRanker(config.ModelStream));
        }

        public static DecisionServiceConfigurationWrapper<TContext, int> WithPolicy<TContext>(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = false;
            return DecisionService.Wrap(config, new VWPolicy<TContext>(config.ModelStream, config.FeatureDiscovery));
        }

        public static DecisionServiceConfigurationWrapper<TContext, int[]> WithRanker<TContext>(DecisionServiceConfiguration config)
        {
            config.UseJsonContext = false;
            return DecisionService.Wrap(config, new VWRanker<TContext>(config.ModelStream, config.FeatureDiscovery));
        }

        public static DecisionServiceConfigurationWrapper<TContext, int[]>
            WithRanker<TContext, TActionDependentFeature>(
                DecisionServiceConfiguration config,
                Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc)
        {
            config.UseJsonContext = false;
            return DecisionService.Wrap(config, new VWRanker<TContext, TActionDependentFeature>(getContextFeaturesFunc, config.ModelStream, config.FeatureDiscovery));
        }


        public static DecisionServiceConfigurationWrapper<TContext, TPolicyValue>
            Wrap<TContext, TPolicyValue>(
                DecisionServiceConfiguration config,
                IContextMapper<TContext, TPolicyValue> vwPolicy)
        {
            var metaData = GetBlobLocations(config);
            var ucm = new DecisionServiceConfigurationWrapper<TContext, TPolicyValue>
            {
                Configuration = config,
                Metadata = metaData
            };

            // conditionally wrap if it can be updated.
            var updatableContextMapper = vwPolicy as IUpdatable<Stream>;

            IContextMapper<TContext, TPolicyValue> policy;

            if (config.OfflineMode || metaData == null || updatableContextMapper == null)
                policy = vwPolicy;
            else
            {
                var dsPolicy = new DecisionServicePolicy<TContext, TPolicyValue>(vwPolicy, config, metaData);
                dsPolicy.Subscribe(ucm);
                policy = dsPolicy;
            }
            ucm.InternalPolicy = policy;

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
