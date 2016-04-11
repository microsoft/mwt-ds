using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using VW;

namespace ClientDecisionServiceTest
{
    public class MockCommandCenter
    {
        public MockCommandCenter(string token)
        {
            this.token = token;
            SetRedirectionBlobLocation();
        }

        public static void SetRedirectionBlobLocation()
        {
            Assembly assembly = typeof(DecisionService).Assembly;
            Type dsct = assembly.GetType("Microsoft.Research.MultiWorldTesting.ClientLibrary.DecisionServiceConstants");
            FieldInfo rblf = dsct.GetField("RedirectionBlobLocation", BindingFlags.Public | BindingFlags.Static);
            rblf.SetValue(null, MockCommandCenter.RedirectionBlobLocation);
        }

        public static void UnsetRedirectionBlobLocation()
        {
            Assembly assembly = typeof(DecisionService).Assembly;
            Type dsct = assembly.GetType("Microsoft.Research.MultiWorldTesting.ClientLibrary.DecisionServiceConstants");
            FieldInfo rblf = dsct.GetField("RedirectionBlobLocation", BindingFlags.Public | BindingFlags.Static);
            rblf.SetValue(null, "http://decisionservicestorage.blob.core.windows.net/app-locations/{0}");
        }

        public void CreateBlobs(bool createSettingsBlob, bool createModelBlob, int modelId = 1)
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
                    byte[] modelContent = this.GetCBModelBlobContent(modelId);
                    modelBlob.UploadFromByteArray(modelContent, 0, modelContent.Length);
                    this.localAzureModelBlobUri = modelBlob.Uri.ToString();
                }

                var locationContainer = blobClient.GetContainerReference(this.localAzureBlobLocationContainerName);
                locationContainer.CreateIfNotExists();

                var publicAccessPermission = new BlobContainerPermissions()
                {
                    PublicAccess = BlobContainerPublicAccessType.Blob
                };

                locationContainer.SetPermissions(publicAccessPermission);

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

        public byte[] GetCBModelBlobContent(int modelId = 1)
        {
            string modelFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "vw" + modelId + ".model");
            return File.ReadAllBytes(modelFile);
        }

        public byte[] GetCBADFModelBlobContent(int modelId = 1)
        {
            string modelFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "vw" + modelId + ".model");
            return File.ReadAllBytes(modelFile);
        }

        public byte[] GetCBModelBlobContent(int numExamples, int numFeatures, int numActions)
        {
            Random rg = new Random(numExamples + numFeatures);

            string localOutputDir = "test";
            string vwFileName = Path.Combine(localOutputDir, string.Format("test_vw_{0}.model", numExamples));
            string vwArgs = "--cb " + numActions;

            using (var vw = new VowpalWabbit<TestRcv1Context>(vwArgs))
            {
                // Create examples
                for (int ie = 0; ie < numExamples; ie++)
                {
                    // Create features
                    var context = TestRcv1Context.CreateRandom(numActions, numFeatures, rg);
                    vw.Learn(context, context.Label);
                }

                vw.Native.SaveModel(vwFileName);
            }

            byte[] vwModelBytes = File.ReadAllBytes(vwFileName);

            Directory.Delete(localOutputDir, recursive: true);

            return vwModelBytes;
        }

        public byte[] GetCBADFModelBlobContent(int numExamples, int numFeatureVectors)
        {
            Random rg = new Random(numExamples + numFeatureVectors);

            string localOutputDir = "test";
            string vwFileName = Path.Combine(localOutputDir, string.Format("test_vw_{0}.model", numExamples));
            string vwArgs = "--cb_adf --rank_all";

            using (var vw = new VowpalWabbit<TestADFContextWithFeatures, TestADFFeatures>(vwArgs))
            {
                //Create examples
                for (int ie = 0; ie < numExamples; ie++)
                {
                    // Create features
                    var context = TestADFContextWithFeatures.CreateRandom(numFeatureVectors, rg);
                    if (ie == 0)
                    {
                        context.Shared = new string[] { "s_1", "s_2" };
                    }

                    vw.Learn(
                        context,
                        context.ActionDependentFeatures,
                        context.ActionDependentFeatures.IndexOf(f => f.Label != null),
                        context.ActionDependentFeatures.First(f => f.Label != null).Label);
                }

                vw.Native.ID = "random_id";
                vw.Native.SaveModel(vwFileName);
            }

            byte[] vwModelBytes = File.ReadAllBytes(vwFileName);

            Directory.Delete(localOutputDir, recursive: true);

            return vwModelBytes;
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

        public static readonly string RedirectionBlobLocation = "http://127.0.0.1:10000/devstoreaccount1/app-locations/{0}";
    }
}