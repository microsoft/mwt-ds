using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VW;

[assembly: InternalsVisibleTo("ClientDecisionServiceTest")]

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Factory class.
    /// </summary>
    public static class DecisionService
    {
        private static ApplicationClientMetadata DownloadMetadata(DecisionServiceConfiguration config, ApplicationClientMetadata metaData)
        {
            if (!config.OfflineMode || metaData == null)
            {
                metaData = ApplicationMetadataUtil.DownloadMetadata<ApplicationClientMetadata>(config.SettingsBlobUri);
                if (config.LogAppInsights)
                {
                    Trace.Listeners.Add(new ApplicationInsights.TraceListener.ApplicationInsightsTraceListener(metaData.AppInsightsKey));
                }
            }

            return metaData;
        }

        public static DecisionServiceClient<TContext> Create<TContext>(DecisionServiceConfiguration config, ITypeInspector typeInspector = null, ApplicationClientMetadata metaData = null)
        {
            return new DecisionServiceClient<TContext>(
                config,
                DownloadMetadata(config, metaData),
                new VWExplorer<TContext>(config.ModelStream, typeInspector, config.DevelopmentMode));
        }

        public static DecisionServiceClient<TContext> Create<TContext>(DecisionServiceConfiguration config, IContextMapper<TContext,ActionProbability[]> contextMapper, ApplicationClientMetadata metaData = null)
        {
            return new DecisionServiceClient<TContext>(
                config,
                DownloadMetadata(config, metaData),
                contextMapper);
        }

        public static DecisionServiceClient<string> CreateJson(DecisionServiceConfiguration config, ApplicationClientMetadata metaData = null)
        {
            return new DecisionServiceClient<string>(
                config,
                DownloadMetadata(config, metaData),
                new VWJsonExplorer(config.ModelStream, config.DevelopmentMode));
        }
    }
}
