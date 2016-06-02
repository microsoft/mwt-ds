using Microsoft.Research.MultiWorldTesting.Contract;
using System;
using System.Collections.Generic;

namespace DecisionServicePrivateWeb.Models
{
    public class SettingsViewModel
    {
        public int ApplicationKey { get; set; }
        public string ApplicationId { get; set; }
        public string AzureSubscriptionName { get; set; }
        public string AzureSubscriptionId { get; set; }
        public string AzureResourceGroupName { get; set; }
        public string ApplicationInsightsName { get; set; }
        public string OnlineTrainerName { get; set; }
        public string WebApiName { get; set; }
        public string WebManageName { get; set; }
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
        public List<BlobModelViewModel> ModelIdList { get; set; }
        public string SelectedModelId { get; set; }
        public bool IsExplorationEnabled { get; set; }
        public Guid AuthorizationToken { get; set; }
    }

    public class BlobModelViewModel
    {
        public string Name { get; set; }
        public string LastModifiedRelativeTime { get; set; }
    }
}