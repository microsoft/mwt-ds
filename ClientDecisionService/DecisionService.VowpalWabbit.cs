using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using VW;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DecisionService_VowpalWabbit
    {
		private static DecisionServiceClient<TContext, int, int> CreateClient<TContext>(
            this DecisionServiceClientSpecification<int> spec,
            IContextMapper<TContext, int> internalPolicy)
        {
            ApplicationTransferMetadata metaData = null;
            int numberOfActions;

            if (!spec.Config.OfflineMode)
            {
                metaData = GetBlobLocations(spec.Config);

                if (spec.NumberOfActions != null)
                    numberOfActions = (int)spec.NumberOfActions;
                else
                {
                    if (metaData.NumActions == null)
                        throw new ArgumentNullException("NumberOfActions is missing in application meta data");

                    numberOfActions = (int)metaData.NumActions;
                }
            }
            else
            {
                if (spec.NumberOfActions == null)
                    throw new ArgumentNullException("Need to provide NumberOfActions in offline mode");

                numberOfActions = (int)spec.NumberOfActions;
            }

			// have sensible defaults
            return new DecisionServiceClient<TContext, int, int>(
                        spec.Config,
                        metaData,
						new EpsilonGreedyExplorer(.1f),
						internalPolicy,
						initialExplorer: new UniformRandomExploration(),
                        numActions: numberOfActions);
		}

        private static DecisionServiceClient<TContext, int[], int[]> CreateClient<TContext>(
            this DecisionServiceClientSpecification<int[]> spec,
            IContextMapper<TContext, int[]> internalPolicy)
        {
            ApplicationTransferMetadata metaData = null;

            if (!spec.Config.OfflineMode)
                metaData = GetBlobLocations(spec.Config);

			// have sensible defaults
            return new DecisionServiceClient<TContext, int[], int[]>(
                        spec.Config,
                        metaData,
                        new TopSlotExplorer(new EpsilonGreedyExplorer(.1f)),
                        internalPolicy,
                        initialExplorer: new PermutationExplorer(1));
        }

        /// <summary>
        /// JSON policy
        /// </summary>
        public static DecisionServiceClient<string, int, int> WithJson(this DecisionServiceClientSpecification<int> spec)
        {
			spec.Config.UseJsonContext = true;
            return spec.CreateClient(new VWJsonPolicy(spec.Config.ModelStream));
        }

        /// <summary>
        /// JSON ranker
        /// </summary>
        public static DecisionServiceClient<string, int[], int[]> WithJson(this DecisionServiceClientSpecification<int[]> spec)
        {
            spec.Config.UseJsonContext = true;
            return spec.CreateClient(new VWJsonRanker(spec.Config.ModelStream));
        }

        /// <summary>
        /// User-defined type policy
        /// </summary>
        public static DecisionServiceClient<TContext, int, int> With<TContext>(this DecisionServiceClientSpecification<int> spec, VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json)
        {
            spec.Config.UseJsonContext = false;
            return spec.CreateClient(new VWPolicy<TContext>(spec.Config.ModelStream, featureDiscovery));
        }

        /// <summary>
        /// User-defined type ranker
        /// </summary>
        public static DecisionServiceClient<TContext, int[], int[]> With<TContext>(this DecisionServiceClientSpecification<int[]> spec, VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json)
        {
            spec.Config.UseJsonContext = false;
            return spec.CreateClient(new VWRanker<TContext>(spec.Config.ModelStream, featureDiscovery));
        }

        /// <summary>
        /// User-defined type ranker
        /// </summary>
        public static DecisionServiceClient<TContext, int[], int[]> With<TContext, TActionDependentFeature>(
            this DecisionServiceClientSpecification<int[]> spec,
            Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc, 
            VowpalWabbitFeatureDiscovery featureDiscovery = VowpalWabbitFeatureDiscovery.Json)
        {
            spec.Config.UseJsonContext = false;
            return spec.CreateClient(
                    new VWRanker<TContext, TActionDependentFeature>(
						getContextFeaturesFunc,
                        spec.Config.ModelStream, 
                        featureDiscovery));
        }

        private static ApplicationTransferMetadata GetBlobLocations(DecisionServiceConfiguration config)
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
