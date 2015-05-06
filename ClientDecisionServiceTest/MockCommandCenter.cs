using Microsoft.Research.DecisionService.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientDecisionServiceTest
{
    public class MockCommandCenter
    {
        public MockCommandCenter(string token)
        {
            this.token = token;
        }

        public void CreateBlobs(bool createSettingsBlob, bool createModelBlob)
        {
            if (createSettingsBlob || createModelBlob)
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(MockCommandCenter.StorageConnectionString);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                var localContainer = blobClient.GetContainerReference(this.localAzureContainerName);
                localContainer.CreateIfNotExists();

                if (createSettingsBlob)
                {
                    var settingsBlob = localContainer.GetBlockBlobReference(this.localAzureSettingsBlobName);
                    byte[] settingsContent = this.GetSettingsBlobContent();
                    settingsBlob.UploadFromByteArray(settingsContent, 0, settingsContent.Length);
                    this.localAzureSettingsBlobUri = settingsBlob.Uri.ToString();
                }

                if (createModelBlob)
                {
                    var modelBlob = localContainer.GetBlockBlobReference(this.localAzureModelBlobName);
                    byte[] modelContent = this.GetModelBlobContent();
                    modelBlob.UploadFromByteArray(modelContent, 0, modelContent.Length);
                    this.localAzureModelBlobUri = modelBlob.Uri.ToString();
                }

                var locationContainer = blobClient.GetContainerReference(this.localAzureBlobLocationContainerName);
                locationContainer.CreateIfNotExists();
                var metadata = new ApplicationTransferMetadata
                {
                    ApplicationID = "test",
                    ConnectionString = MockCommandCenter.StorageConnectionString,
                    ExperimentalUnitDuration = 15,
                    IsExplorationEnabled = true,
                    ModelBlobUri = this.localAzureModelBlobUri,
                    SettingsBlobUri = this.localAzureSettingsBlobUri,
                    ModelId = "latest"
                };

                var locationBlob = locationContainer.GetBlockBlobReference(this.token);
                byte[] locationBlobContent = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metadata));
                locationBlob.UploadFromByteArray(locationBlobContent, 0, locationBlobContent.Length);
            }
        }

        public byte[] GetSettingsBlobContent()
        {
            return new byte[3] { 1, 2, 3 };
        }

        public byte[] GetModelBlobContent()
        {
            return new byte[2] { 5, 1 };
        }

        public string LocalAzureSettingsBlobName
        {
            get { return localAzureSettingsBlobName; }
        }

        public string LocalAzureModelBlobName
        {
            get { return localAzureModelBlobName; }
        }

        private string token;
        private string localAzureSettingsBlobUri;
        private string localAzureModelBlobUri;

        private readonly string localAzureBlobLocationContainerName = "app-locations";
        private readonly string localAzureContainerName = "localtestcontainer";
        private readonly string localAzureSettingsBlobName = "localtestsettingsblob";
        private readonly string localAzureModelBlobName = "localtestmodelblob";

        public static readonly string StorageConnectionString = "UseDevelopmentStorage=true";
        public static readonly string AuthorizationToken = "test token";
    }
}
