//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Research.MultiWorldTesting.Contract
{
    /// <summary>
    /// The type of decision.
    /// </summary>
    public enum DecisionType
    {
        /// <summary>
        /// Choose a single action from a fixed set of available actions.
        /// </summary>
        SingleAction = 0,

        /// <summary>
        /// Choose multiple actions from a variable set of available actions.
        /// </summary>
        MultiActions
    }

    /// <summary>
    /// The frequency with which a new model needs to be retrained.
    /// </summary>
    public enum TrainFrequency
    {
        Low = 0,
        High
    }

    public class ApplicationClientMetadata
    {
        /// <summary>
        /// The name of the application as created on the Command Center.
        /// </summary>
        public string ApplicationID { get; set; }

        /// <summary>
        /// Number of actions available at decision time, this is only relevant if decision type is single.
        /// </summary>
        public int? NumActions { get; set; }

        /// <summary>
        /// Additional arguments to be used in training service.
        /// </summary>
        public string AdditionalTrainArguments { get; set; }

        /// <summary>
        /// The EventHub connection string to which the client needs to send interaction data.
        /// </summary>
        public string EventHubInteractionConnectionString { get; set; }

        /// <summary>
        /// The EventHub connection string to which the client needs to send interaction data.
        /// </summary>
        public string EventHubObservationConnectionString { get; set; }

        /// <summary>
        /// Turn on/off exploration at client side.
        /// </summary>
        public bool IsExplorationEnabled { get; set; }

        /// <summary>
        /// The publicly accessible Uri of the model blob that clients can check for update.
        /// </summary>
        public string ModelBlobUri { get; set; }
    }

    public class ApplicationTrainerMetadata
    {
        /// <summary>
        /// The name of the application as created on the Command Center.
        /// </summary>
        public string ApplicationID { get; set; }

        /// <summary>
        /// Additional arguments to be used in training service.
        /// </summary>
        public string AdditionalTrainArguments { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        public string EventHubListenConnectionString { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string EventHubEvalConnectionString { get; set; }

        /// <summary>
        /// The connection string for the provisioned Azure storage account.
        /// </summary>
        public string ConnectionString { get; set; }
    }

    public class ApplicationExtraMetadata
    {
        /// <summary>
        /// The Id of the Azure subscription associated with this application.
        /// </summary>
        public string SubscriptionId { get; set; }

        /// <summary>
        /// The name of the Azure resource group provisioned for this application.
        /// </summary>
        public string AzureResourceGroupName { get; set; }

        /// <summary>
        /// The type of decision.
        /// </summary>
        public DecisionType DecisionType { get; set; }
        
        /// <summary>
        /// The training frequency type.
        /// </summary>
        public TrainFrequency TrainFrequency { get; set; }

        /// <summary>
        /// The Id of the model to use in client library.
        /// </summary>
        public string ModelId { get; set; }

        /// <summary>
        /// The experimental unit duration for joining decisions with observations.
        /// </summary>
        public int ExperimentalUnitDuration { get; set; }
    }
}
