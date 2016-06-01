using DecisionServicePrivateWeb.Models;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DecisionServicePrivateWeb.Classes
{
    public class ApplicationMetadataStore
    {
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
                    ModelBlobUri = appSettings.ModelBlobUri
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