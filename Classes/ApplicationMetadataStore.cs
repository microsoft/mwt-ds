using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Web;

namespace DecisionServicePrivateWeb.Classes
{
    public class ApplicationMetadataStore
    {
        public const string AKAzureResourceGroup = "resourceGroupName";
        public const string AKConnectionString = "AzureStorageConnectionString";
        public const string AKPassword = "Password";
        public const string AKInitialExplorationEpsilon = "initialExplorationEpsilon";
        public const string AKInterEHSendConnString = "interactionEventHubSendConnectionString";
        public const string AKObserEHSendConnString = "observationEventHubSendConnectionString";
        public const string AKJoinedEHSendConnString = "joinedEventHubConnectionString";
        public const string AKEvalEHSendConnString = "evalEventHubConnectionString";
        public const string AKAdminToken = "adminToken";
        public const string AKTrainerURL = "trainerURL";
        public const string AKWebServiceToken = "webServiceToken";
        public const string AKCheckpointPolicy = "checkpointPolicy";
        public const string AKTrainArguments = "vowpalWabbitTrainArguments";
        public const string AKNumActions = "numberOfActions";
        public const string AKSubscriptionId = "subscriptionId";
        public const string AKExpUnitDuration = "experimentalUnitDurationInSeconds";
        public const string AKAppInsightsKey = "APPINSIGHTS_INSTRUMENTATIONKEY";
        public const string AKDecisionServiceSettingsUrl = "DecisionServiceSettingsUrl";

        public const string StorageARMDeployContainer = "arm-deploy";
        public const string StorageOnlineTrainerPackageName = "OnlineTraining.cspkg";

        public static void CreateSettingsBlobIfNotExists(out string clSASTokenUri, out string webSASTokenUri)
        {
            clSASTokenUri = null;
            webSASTokenUri = null;

            var telemetry = new TelemetryClient();

            var sasPolicy = new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-1),
                SharedAccessExpiryTime = DateTime.UtcNow.AddYears(1)
            };

            string azureStorageConnectionString = ConfigurationManager.AppSettings[AKConnectionString];
            var storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var settingsBlobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.SettingsContainerName);
            try
            {
                var modelBlobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.ModelContainerName);

                var clientSettingsBlob = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestClientSettingsBlobName);
                var extraSettingsBlob = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestExtraSettingsBlobName);
                if (!clientSettingsBlob.Exists() || !extraSettingsBlob.Exists())
                {
                    telemetry.TrackTrace("Settings blob not found, creating new one.");

                    settingsBlobContainer.CreateIfNotExists();
                    modelBlobContainer.CreateIfNotExists();

                    // Create an empty model blob to generate SAS token for
                    var modelBlob = modelBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestModelBlobName);
                    modelBlob.UploadText(string.Empty);
                    var modelSASToken = modelBlob.GetSharedAccessSignature(sasPolicy);

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
                        ModelId = ApplicationSettingConstants.UseLatestModelSetting,
                        TrainArguments = ConfigurationManager.AppSettings[AKTrainArguments],
                        TrainFrequency = TrainFrequency.High, // TODO: update depending on deployment option
                        ModelBlobUri = modelBlob.Uri + modelSASToken,
                        AppInsightsKey = ConfigurationManager.AppSettings[AKAppInsightsKey],
                        InitialExplorationEpsilon = Convert.ToSingle(ConfigurationManager.AppSettings[AKInitialExplorationEpsilon])
                    };
                    UpdateMetadata(clientSettingsBlob, extraSettingsBlob, appSettings);

                    var clSASToken = clientSettingsBlob.GetSharedAccessSignature(sasPolicy);
                    var webSASToken = clientSettingsBlob.GetSharedAccessSignature(sasPolicy);

                    clSASTokenUri = clientSettingsBlob.Uri + clSASToken;
                    webSASTokenUri = clientSettingsBlob.Uri + webSASToken;

                    appSettings.SettingsTokenUri1 = clSASTokenUri;
                    appSettings.SettingsTokenUri2 = webSASTokenUri;
                    UpdateMetadata(clientSettingsBlob, extraSettingsBlob, appSettings);

                    telemetry.TrackTrace($"Model blob uri: {appSettings.ModelBlobUri}");
                }
                else
                {
                    telemetry.TrackTrace("Settings blob already exists, skipping.");
                    var extraMetadata = JsonConvert.DeserializeObject<ApplicationExtraMetadata>(extraSettingsBlob.DownloadText());
                    clSASTokenUri = extraMetadata.SettingsTokenUri1;
                    webSASTokenUri = extraMetadata.SettingsTokenUri2;
                }

                telemetry.TrackTrace($"Settings blob uri for client library: {clSASTokenUri}, for web api: {webSASTokenUri}");
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
            }
            
        }

        public static string CreateOnlineTrainerCspkgBlobIfNotExists(string cspkgLink)
        {
            var telemetry = new TelemetryClient();
            string azureStorageConnectionString = ConfigurationManager.AppSettings[AKConnectionString];
            var storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var armDeployContainer = blobClient.GetContainerReference(StorageARMDeployContainer);

            var cspkgBlob = armDeployContainer.GetBlockBlobReference(StorageOnlineTrainerPackageName);
            if (!cspkgBlob.Exists())
            {
                telemetry.TrackTrace("Online Trainer Package blob not found, creating new one.");

                armDeployContainer.CreateIfNotExists();
                using (var wc = new WebClient())
                using (var cspkgStream = new MemoryStream(wc.DownloadData(cspkgLink)))
                {
                    cspkgBlob.UploadFromStream(cspkgStream);
                }
            }
            else
            {
                telemetry.TrackTrace("Online Trainer Package already exists.");
            }
            var sasPolicy = new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-1),
                SharedAccessExpiryTime = DateTime.UtcNow.AddYears(1)
            };

            var uri = cspkgBlob.Uri.ToString() + cspkgBlob.GetSharedAccessSignature(sasPolicy);
            telemetry.TrackTrace($"Online Trainer Package URI: '{uri}'");

            return uri;
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
                    EventHubInteractionConnectionString = appSettings.InterEventHubSendConnectionString,
                    EventHubObservationConnectionString = appSettings.ObserEventHubSendConnectionString,
                    IsExplorationEnabled = appSettings.IsExplorationEnabled,
                    ModelBlobUri = appSettings.ModelBlobUri,
                    TrainArguments = appSettings.TrainArguments,
                    AppInsightsKey = appSettings.AppInsightsKey,
                    InitialExplorationEpsilon = appSettings.InitialExplorationEpsilon
                },
                new ApplicationExtraMetadata
                {
                    AzureResourceGroupName = appSettings.AzureResourceGroupName,
                    DecisionType = appSettings.DecisionType,
                    ExperimentalUnitDuration = appSettings.ExperimentalUnitDuration,
                    SubscriptionId = appSettings.SubscriptionId,
                    ModelId = appSettings.ModelId,
                    TrainFrequency = appSettings.TrainFrequency,
                    SettingsTokenUri1 = appSettings.SettingsTokenUri1,
                    SettingsTokenUri2 = appSettings.SettingsTokenUri2
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