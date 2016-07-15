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
using VW.Serializer;

namespace ClientDecisionServiceTest
{
    public class MockCommandCenter
    {
        public MockCommandCenter()
        {
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

        public void CreateBlobs(bool createSettingsBlob, bool createModelBlob, int modelId = 1, string vwArgs = null, float initialExplorationEpsilon = 0f)
        {
            if (createSettingsBlob || createModelBlob)
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(MockCommandCenter.StorageConnectionString);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                var localContainer = blobClient.GetContainerReference(this.localAzureContainerName);
                localContainer.CreateIfNotExists();
                var publicAccessPermission = new BlobContainerPermissions()
                {
                    PublicAccess = BlobContainerPublicAccessType.Blob
                };
                localContainer.SetPermissions(publicAccessPermission);

                if (createModelBlob)
                {
                    var modelBlob = localContainer.GetBlockBlobReference(this.localAzureModelBlobName);
                    byte[] modelContent = this.GetCBADFModelBlobContent(5, 5, vwArgs);
                    modelBlob.UploadFromByteArray(modelContent, 0, modelContent.Length);
                    this.localAzureModelBlobUri = modelBlob.Uri.ToString();
                }
                else
                    this.localAzureModelBlobUri = NotFoundModelBlobLocation;

                if (createSettingsBlob)
                {
                    var settingsBlob = localContainer.GetBlockBlobReference(this.localAzureSettingsBlobName);
                    byte[] settingsContent = this.GetSettingsBlobContent(vwArgs, initialExplorationEpsilon);
                    settingsBlob.UploadFromByteArray(settingsContent, 0, settingsContent.Length);
                    SettingsBlobUri = settingsBlob.Uri.ToString();
                }

            }
        }

        public byte[] GetSettingsBlobContent(string vwArgs, float initialExplorationEpsilon)
        {
            var metadata = new ApplicationClientMetadata
            {
                ApplicationID = TestAppID,
                IsExplorationEnabled = true,
                ModelBlobUri = this.localAzureModelBlobUri,
                InitialExplorationEpsilon = initialExplorationEpsilon,
                TrainArguments = vwArgs
            };
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metadata));
        }

        public byte[] GetCBModelBlobContent(int numExamples, int numFeatures, int numActions, string vwArgs)
        {
            Random rg = new Random(numExamples + numFeatures);

            string localOutputDir = "test";
            string vwFileName = Path.Combine(localOutputDir, string.Format("test_vw_{0}.model", numExamples));

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

        public byte[] GetCBADFModelBlobContent(int numExamples, int numFeatureVectors, string vwDefaultArgs)
        {
            Random rg = new Random(numExamples + numFeatureVectors);

            string localOutputDir = "test";
            string vwFileName = Path.Combine(localOutputDir, string.Format("test_vw_{0}.model", numExamples));
            string vwArgs = vwDefaultArgs;

            using (var vw = new VowpalWabbit(vwArgs))
            {
                vw.Learn(new[] { "1:-3:0.2 | b:2" });
                vw.ID = "123";
                vw.SaveModel(vwFileName);
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

        private string localAzureModelBlobUri;

        private readonly string localAzureContainerName = "localtestcontainer";
        private readonly string localAzureSettingsBlobName = "localtestsettingsblob";
        private readonly string localAzureModelBlobName = "localtestmodelblob";

        public static string SettingsBlobUri;
        public static readonly string TestAppID = "test app";


        public static readonly string StorageConnectionString;
        public static readonly string NotFoundModelBlobLocation;
        public static readonly string RedirectionBlobLocation;

        static MockCommandCenter()
        {
            var connectionString = Environment.GetEnvironmentVariable("StorageConnectionString");
            if (connectionString == null)
            {
                // use development storage
                StorageConnectionString = "UseDevelopmentStorage=true";
                NotFoundModelBlobLocation = "http://127.0.0.1:10000/devstoreaccount1/localtestcontainer/notfoundmodel";
                RedirectionBlobLocation = "http://127.0.0.1:10000/devstoreaccount1/app-locations/{0}";
            }
            else
            {
                StorageConnectionString = connectionString;
                var account = CloudStorageAccount.Parse(connectionString);
                NotFoundModelBlobLocation = account.BlobStorageUri.PrimaryUri + "localtestcontainer/notfoundmodel";
                RedirectionBlobLocation = account.BlobStorageUri.PrimaryUri + "app-locations/{0}";
            }
        }
    }
}