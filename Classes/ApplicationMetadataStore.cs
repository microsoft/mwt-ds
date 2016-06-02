using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Web;

namespace DecisionServicePrivateWeb.Classes
{
    public class ApplicationMetadataStore : IHttpModule
    {
        public const string AKAzureResourceGroup = "resourceGroupName";
        public const string AKConnectionString = "AzureStorageConnectionString";
        public const string AKPassword = "Password";
        public const string AKInterEHSendConnString = "interactionEventHubSendConnectionString";
        public const string AKObserEHSendConnString = "observationEventHubSendConnectionString";
        public const string AKTrainArguments = "vowpalWabbitTrainArguments";
        public const string AKNumActions = "numberOfActions";
        public const string AKSubscriptionId = "subscriptionId";
        public const string AKExpUnitDuration = "experimentalUnitDurationInSeconds";

        public void Init(HttpApplication context)
        {
            ApplicationMetadataStore.CreateSettingsBlobIfNotExists();
        }

        public void Dispose() { }

        public static void CreateSettingsBlobIfNotExists()
        {
            var telemetry = new TelemetryClient();
            string azureStorageConnectionString = ConfigurationManager.AppSettings[AKConnectionString];
            var storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var settingsBlobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.SettingsContainerName);
            var modelBlobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.ModelContainerName);

            var clientSettingsBlob = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestClientSettingsBlobName);
            var extraSettingsBlob = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestExtraSettingsBlobName);
            if (!clientSettingsBlob.Exists() || !extraSettingsBlob.Exists())
            {
                telemetry.TrackTrace("Settings blob not found, creating new one.");

                settingsBlobContainer.CreateIfNotExists();
                modelBlobContainer.CreateIfNotExists();

                var appSettings = new ApplicationSettings
                {
                    ApplicationID = ConfigurationManager.AppSettings[AKAzureResourceGroup],
                    AzureResourceGroupName = ConfigurationManager.AppSettings[AKAzureResourceGroup],
                    ConnectionString = ConfigurationManager.AppSettings[AKConnectionString],
                    ExperimentalUnitDuration = Convert.ToInt32(ConfigurationManager.AppSettings[AKExpUnitDuration]),
                    InterEventHubSendConnectionString = ConfigurationManager.AppSettings[AKInterEHSendConnString],
                    ObserEventHubSendConnectionString = ConfigurationManager.AppSettings[AKObserEHSendConnString],
                    IsExplorationEnabled = true,
                    SubscriptionId = ConfigurationManager.AppSettings[AKSubscriptionId],
                    DecisionType = DecisionType.MultiActions, // TODO: update depending on deployment option
                    ModelId = ApplicationBlobConstants.LatestModelBlobName,
                    NumActions = Convert.ToInt32(ConfigurationManager.AppSettings[AKNumActions]),
                    TrainArguments = ConfigurationManager.AppSettings[AKTrainArguments],
                    TrainFrequency = TrainFrequency.High, // TODO: update depending on deployment option
                    ModelBlobUri = modelBlobContainer.Uri.ToString() + "/" + ApplicationBlobConstants.LatestModelBlobName,
                    SettingsBlobUri = settingsBlobContainer.Uri.ToString() + "/" + ApplicationBlobConstants.LatestClientSettingsBlobName
                };
                ApplicationMetadataStore.UpdateMetadata(clientSettingsBlob, extraSettingsBlob, appSettings);

                telemetry.TrackTrace($"Model blob uri: {appSettings.ModelBlobUri}");
                telemetry.TrackTrace($"Settings blob uri: {appSettings.SettingsBlobUri}");
            }
        }

        public static void UpdateMetadata(CloudBlockBlob clientSettingsBlob, CloudBlockBlob extraSettingsBlob, ApplicationSettings appSettings)
        {
            try
            {
                var metadata = GetTransferMetadata(appSettings);
                clientSettingsBlob.UploadText(JsonConvert.SerializeObject(metadata.Item1));
                extraSettingsBlob.UploadText(JsonConvert.SerializeObject(metadata.Item2));
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.ToString());
            }
        }

        public static void UpdateModel(string selectedModelId, string azureStorageConnectionString)
        {
            var storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var modelContainer = blobClient.GetContainerReference(ApplicationBlobConstants.ModelContainerName);
            var selectedModelBlob = ApplicationMetadataStore.GetSelectedModelBlob(modelContainer, selectedModelId);
            if (selectedModelBlob != null && modelContainer != null)
            {
                var currentModelBlob = modelContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestModelBlobName);
                currentModelBlob.StartCopy(selectedModelBlob);
            }
        }

        private static Tuple<ApplicationClientMetadata, ApplicationExtraMetadata> GetTransferMetadata(ApplicationSettings appSettings)
        {
            return new Tuple<ApplicationClientMetadata, ApplicationExtraMetadata>(
                new ApplicationClientMetadata
                {
                    ApplicationID = appSettings.ApplicationID,
                    NumActions = appSettings.NumActions,
                    EventHubInteractionConnectionString = appSettings.InterEventHubSendConnectionString,
                    EventHubObservationConnectionString = appSettings.ObserEventHubSendConnectionString,
                    IsExplorationEnabled = appSettings.IsExplorationEnabled,
                    ModelBlobUri = appSettings.ModelBlobUri,
                    TrainArguments = appSettings.TrainArguments
                },
                new ApplicationExtraMetadata
                {
                    AzureResourceGroupName = appSettings.AzureResourceGroupName,
                    DecisionType = appSettings.DecisionType,
                    ExperimentalUnitDuration = appSettings.ExperimentalUnitDuration,
                    SubscriptionId = appSettings.SubscriptionId,
                    ModelId = appSettings.ModelId,
                    TrainFrequency = appSettings.TrainFrequency
                }
            );
        }

        private static bool IsLatestModelBlob(IListBlobItem blob)
        {
            return blob is CloudBlockBlob && string.Compare(((CloudBlockBlob)blob).Name, ApplicationBlobConstants.LatestModelBlobName, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static CloudBlockBlob GetSelectedModelBlob(CloudBlobContainer blobContainer, string modelId, bool forceLatest = false)
        {
            CloudBlockBlob blockBlob = null;

            bool useLatestModel = forceLatest | String.Equals(modelId, ApplicationSettingConstants.UseLatestModelSetting, StringComparison.OrdinalIgnoreCase);

            if (!useLatestModel) // If not latest, use the selected model
            {
                if (!String.IsNullOrWhiteSpace(modelId))
                {
                    blockBlob = blobContainer.GetBlockBlobReference(modelId);
                }
            }
            else
            {
                DateTimeOffset lastBlobDate = new DateTimeOffset();
                IEnumerable<IListBlobItem> blobs = blobContainer.ListBlobs();
                foreach (IListBlobItem blobItem in blobs)
                {
                    if (blobItem is CloudBlockBlob && !IsLatestModelBlob(blobItem))
                    {
                        var bbItem = (CloudBlockBlob)blobItem;
                        DateTimeOffset bbDate = bbItem.Properties.LastModified.GetValueOrDefault();
                        if (bbDate >= lastBlobDate)
                        {
                            blockBlob = bbItem;
                            lastBlobDate = bbDate;
                        }
                    }
                }
            }

            return blockBlob;
        }
    }
}