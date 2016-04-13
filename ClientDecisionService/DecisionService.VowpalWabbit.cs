using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class DecisionService_VowpalWabbit
    {
		private static DecisionServiceClient<TContext, int, TPolicyValue> CreateClient<TContext, TPolicyValue>(
            this DecisionServiceClientSpecification<int> spec,
			DecisionServiceConfigurationWrapper<TContext, TPolicyValue> configWrapper)
        {
			// have sensible defaults
            return new DecisionServiceClient<TContext, int, TPolicyValue>(
					new DecisionServiceClientInternal<TContext, int, TPolicyValue>(
                        spec.config,
						configWrapper.Metadata,
						new EpsilonGreedyExplorer(.1f, spec.NumberOfActions),
						configWrapper.InternalPolicy,
						initialExplorer: new UniformRandomExploration()));
		}

        private static DecisionServiceClient<TContext, int[], TPolicyValue> CreateClient<TContext, TPolicyValue>(
            this DecisionServiceClientSpecification<int[]> spec,
            DecisionServiceConfigurationWrapper<TContext, TPolicyValue> configWrapper)
        {
			// have sensible defaults
            return new DecisionServiceClient<TContext, int[], TPolicyValue>(
                    new DecisionServiceClientInternal<TContext, int[], TPolicyValue>(
                        spec.config,
                        configWrapper.Metadata,
                        new TopSlotExplorer(new EpsilonGreedyExplorer(.1f),
                        configWrapper.InternalPolicy,
                        initialExplorer: new PermutationExplorer(1))));
        }

        /// <summary>
        /// JSON policy
        /// </summary>
        public static DecisionServiceClient<int, string, int> WithJson(this DecisionServiceClientSpecification<int> spec)
        {
            var config = spec.Config;
			config.UseJsonContext = true;
            
            return spec.CreateClient(DecisionService.Wrap(config, new VWJsonPolicy(config.ModelStream)));
        }

        /// <summary>
        /// JSON ranker
        /// </summary>
        public static DecisionServiceClient<int[], string, int[]> WithJson(this DecisionServiceClientSpecification<int[]> spec)
        {
            var config = spec.Config;
            config.UseJsonContext = true;

            spec.CreateClient(DecisionService.Wrap(config, new VWJsonRanker(config.ModelStream)));
        }

        /// <summary>
        /// User-defined type policy
        /// </summary>
        public static DecisionServiceClient<int, string, int> With<TContext>(this DecisionServiceClientSpecification<int> spec)
        {
            var config = spec.Config;
            config.UseJsonContext = false;

            return spec.CreateClient(DecisionService.Wrap(config, new VWPolicy<TContext>(config.ModelStream, config.FeatureDiscovery)));
        }

        /// <summary>
        /// User-defined type ranker
        /// </summary>
        public static DecisionServiceClient<int[], string, int[]> With<TContext>(this DecisionServiceClientSpecification<int[]> spec)
        {
            var config = spec.Config;
            config.UseJsonContext = false;

            spec.CreateClient(DecisionService.Wrap(config, new VWRanker<TContext>(config.ModelStream, config.FeatureDiscovery)));
        }

        /// <summary>
        /// User-defined type ranker
        /// </summary>
        public static DecisionServiceClient<int[], string, int[]> With<TContext, TActionDependentFeature>(
            this DecisionServiceClientSpecification<int[]> spec,
             Func<TContext, IReadOnlyCollection<TActionDependentFeature>> getContextFeaturesFunc)
        {
            var config = spec.Config;
            config.UseJsonContext = false;

            spec.CreateClient(
                DecisionService.Wrap(
					config, 
                    new VWRanker<TContext, TActionDependentFeature>(
						getContextFeaturesFunc,
                        config.ModelStream, 
                        config.FeatureDiscovery)));
        }
    }
}
