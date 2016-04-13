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
    /// Incomplete decision service client, just a vehicle to transport TAction
    /// </summary>
    /// <typeparam name="TAction"></typeparam>
    public class DecisionServiceClientSpecification<TAction>
    {
        internal int? NumberOfActions;

        internal DecisionServiceConfiguration Config;
    }

    /// <summary>
    /// Factory class.
    /// </summary>
    public static class DecisionService
    {
        public static DecisionServiceClientSpecification<int> WithPolicy(DecisionServiceConfiguration config, int numberOfActions)
        { 
            return new DecisionServiceClientSpecification<int>()
            {
                Config = config,
                NumberOfActions = numberOfActions
            };
        }

        public static DecisionServiceClientSpecification<int[]> WithRanker(DecisionServiceConfiguration config)
        { 
            return new DecisionServiceClientSpecification<int[]>()
            {
                Config = config
            };
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
