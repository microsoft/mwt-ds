//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Research.MultiWorldTesting.Contract
{
    public enum DecisionType
    {
        SingleAction = 0,
        MultiActions
    }

    public enum TrainFrequency
    {
        Low = 0,
        High
    }

    public class ApplicationTransferMetadata
    {
        public string ApplicationID { get; set; }
        public string SubscriptionId { get; set; }

        public DecisionType DecisionType { get; set; }

        public int? NumActions { get; set; }

        public TrainFrequency TrainFrequency { get; set; }

        public string EventHubConnectionString { get; set; }

        public string EventHubInputName { get; set; }

        public string ConnectionString { get; set; }

        public string ModelId { get; set; }

        public int ExperimentalUnitDuration { get; set; }

        public bool IsExplorationEnabled { get; set; }

        public string ModelBlobUri { get; set; }

        public string SettingsBlobUri { get; set; }
    }
}
