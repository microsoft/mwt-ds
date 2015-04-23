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
    public class MockCommandCenter : MockHttpServer
    {
        public MockCommandCenter(string uri) : base(uri) { }

        public void CreateBlobs(bool createSettingsBlob, bool createModelBlob)
        {
            if (createSettingsBlob || createModelBlob)
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse("UseDevelopmentStorage=true");
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

        protected override void Listen()
        {
            this.cancelTokenSource.Token.ThrowIfCancellationRequested();

            while (!this.cancelTokenSource.Token.IsCancellationRequested)
            {
                HttpListenerContext ccContext = listener.GetContext();
                HttpListenerRequest request = ccContext.Request;

                //Response object
                HttpListenerResponse response = ccContext.Response;

                //Construct response
                if (request.RawUrl.ToLower().Contains("/application/getmetadata?token=test%20token")
                    && request.HttpMethod == "GET")
                {
                    var metadata = new ApplicationTransferMetadata
                    {
                        ApplicationID = "test",
                        ConnectionString = "UseDevelopmentStorage=true",
                        ExperimentalUnitDuration = 15,
                        IsExplorationEnabled = true,
                        ModelBlobUri = this.localAzureModelBlobUri,
                        SettingsBlobUri = this.localAzureSettingsBlobUri,
                        ModelId = "latest"
                    };
                    string responseMessage = JsonConvert.SerializeObject(metadata);

                    string requestBody;
                    Stream iStream = request.InputStream;
                    Encoding encoding = request.ContentEncoding;
                    StreamReader reader = new StreamReader(iStream, encoding);
                    requestBody = reader.ReadToEnd();

                    Console.WriteLine("POST request on {0} with body = [{1}]", request.RawUrl, requestBody);

                    byte[] buffer = Encoding.UTF8.GetBytes(responseMessage);
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentEncoding = System.Text.Encoding.UTF8;
                    response.ContentType = "text/plain";
                    response.ContentLength64 = buffer.Length;

                    //Return a response
                    using (Stream stream = response.OutputStream)
                    {
                        stream.Write(buffer, 0, buffer.Length);
                    }

                    response.Close();
                }
                else
                {
                    throw new Exception("Invalid Get Request: " + request.RawUrl);
                }
            }
        }

        public override void Reset()
        {
            base.Reset();

            this.localAzureSettingsBlobUri = null;
            this.localAzureModelBlobUri = null;
        }

        private string localAzureSettingsBlobUri;
        private string localAzureModelBlobUri;

        private readonly string localAzureContainerName = "localtestcontainer";
        private readonly string localAzureSettingsBlobName = "localtestsettingsblob";
        private readonly string localAzureModelBlobName = "localtestmodelblob";
    }
}
