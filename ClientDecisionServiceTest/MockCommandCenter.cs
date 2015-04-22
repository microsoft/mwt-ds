using Microsoft.Research.DecisionService.Common;
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
                        ConnectionString = "",
                        ExperimentalUnitDuration = 15,
                        IsExplorationEnabled = true,
                        ModelBlobUri = null,
                        SettingsBlobUri = null,
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
    }
}
