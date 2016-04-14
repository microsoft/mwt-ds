using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
			// have sensible defaults
            return new DecisionServiceClient<TContext, int, int>(
                        spec.Config,
						new EpsilonGreedyExplorer(.1f, (int)spec.NumberOfActions),
						internalPolicy,
						initialExplorer: new UniformRandomExploration());
		}

        private static DecisionServiceClient<TContext, int[], int[]> CreateClient<TContext>(
            this DecisionServiceClientSpecification<int[]> spec,
            IContextMapper<TContext, int[]> internalPolicy)
        {
			// have sensible defaults
            return new DecisionServiceClient<TContext, int[], int[]>(
                        spec.Config,
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
    }
}
