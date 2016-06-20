using DecisionServicePrivateWeb.Classes;
using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;

namespace DecisionServicePrivateWeb.Controllers
{
    public static class DecisionServiceClientFactory
    {
        public static DecisionServiceClient<string> AddOrGetExisting(Action<byte[]> modelSuccessNotifier)
        {
            return DecisionServiceStaticClient.AddOrGetExisting("single", _ =>
            {
                var telemetry = new TelemetryClient();
                string azureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString];

                var storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
                var blobClient = storageAccount.CreateCloudBlobClient();
                var settingsBlobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.SettingsContainerName);
                var clientSettingsBlob = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestClientSettingsBlobName);

                //var settingsUrl = clientSettingsBlob.StorageUri.PrimaryUri.ToString();
                var settingsUrl = APIUtil.GetSettingsUrl();

                telemetry.TrackEvent($"DecisionServiceClient created: '{settingsUrl}'");

                var config = new DecisionServiceConfiguration(settingsUrl)
                {
                    InteractionUploadConfiguration = new BatchingConfiguration
                    {
                        // TODO: these are not production ready configurations. do we need to move those to C&C as well?
                        MaxBufferSizeInBytes = 1,
                        MaxDuration = TimeSpan.FromSeconds(1),
                        MaxEventCount = 1,
                        MaxUploadQueueCapacity = 1,
                        UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
                    },
                    ModelPollSuccessCallback = modelSuccessNotifier,
                    ModelPollFailureCallback = e => telemetry.TrackException(e, new Dictionary<string, string> { { "Pool failure", "model" } }),
                    SettingsPollFailureCallback = e => telemetry.TrackException(e, new Dictionary<string, string> { { "Pool failure", "settings" } }),
                    AzureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString]
                };

                return DecisionService.CreateJson(config);
            });
        }
    }
}