using Microsoft.Research.MultiWorldTesting.Contract;

namespace DecisionServicePrivateWeb.Classes
{
    public class ApplicationSettings
    {
        public string SubscriptionId { get; set; }

        public string AzureResourceGroupName { get; set; }
        public string AppInsightsKey { get; set; }
        public DecisionType DecisionType { get; set; }

        public int? NumActions { get; set; }

        public TrainFrequency TrainFrequency { get; set; }

        public string TrainArguments { get; set; }

        public string ApplicationID { get; set; }

        public string ConnectionString { get; set; }

        public string InterEventHubSendConnectionString { get; set; }

        public string ObserEventHubSendConnectionString { get; set; }

        public string ModelId { get; set; }

        public float InitialExplorationEpsilon { get; set; }

        public bool IsExplorationEnabled { get; set; }

        public int ExperimentalUnitDuration { get; set; }

        public string ModelBlobUri { get; set; }

        public string SettingsTokenUri1 { get; set; }

        public string SettingsTokenUri2 { get; set; }
    }
}