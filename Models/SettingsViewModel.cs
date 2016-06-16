using Microsoft.Research.MultiWorldTesting.Contract;
using System;
using System.Collections.Generic;

namespace DecisionServicePrivateWeb.Models
{
    public class CollectiveSettingsView
    {
        public string ApplicationId { get; set; }
        public string AzureSubscriptionId { get; set; }
        public string AzureResourceGroupName { get; set; }
        public string ApplicationInsightsInstrumentationKey { get; set; }
        public string SettingsBlobUri { get; set; }
        public string OnlineTrainerAddress { get; set; }
        public string WebApiAddress { get; set; }
        public string ASAEvalName { get; set; }
        public string ASAJoinName { get; set; }
        public DecisionType DecisionType { get; set; }
        public int? NumActions { get; set; }
        public TrainFrequency TrainFrequency { get; set; }
        public string TrainArguments { get; set; }
        public string AzureStorageConnectionString { get; set; }
        public string EventHubInteractionConnectionString { get; set; }
        public string EventHubObservationConnectionString { get; set; }
        public int ExperimentalUnitDuration { get; set; }
        public SettingBlobListViewModel SelectedModelId { get; set; }
        public bool IsExplorationEnabled { get; set; }
        public float InitialExplorationEpsilon { get; set; }
        public Dictionary<string, List<string>> NameHelpLink { get; set; }
    }

    public class BlobModelViewModel
    {
        public string Name { get; set; }
        public string LastModifiedRelativeTime { get; set; }
    }

    public class SettingItemViewModel
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public object Value { get; set; }

        public string HelpText { get; set; }

        public string Url { get; set; }

        public string UrlToolTip { get; set; }

        public bool? IsEditable { get; set; }

        public bool? IsSplotlightUrl { get; set; }

        public bool? IsVisible { get; set; }

        public bool? IsResettable { get; set; }
    }

    public class SettingBlobListViewModel
    {
        public List<BlobModelViewModel> Items { get; set; }

        public string SelectedItem { get; set; }
    }
}