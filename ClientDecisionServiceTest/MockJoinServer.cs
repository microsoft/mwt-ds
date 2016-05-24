using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
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
    public class MockJoinServer : MockHttpServer
    {
        public MockJoinServer(string uri) : base(uri) 
        {
            EventBatchList = new List<PartialDecisionServiceMessage>();
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
                if (request.RawUrl.ToLower().Contains("/join")
                    && request.HttpMethod == "POST")
                {
                    Interlocked.Increment(ref requestCount);

                    Authorization auth = new Authorization(request.Headers);
                    switch (auth.Type)
                    {
                        case AuthorizationType.DecisionService:
                            if (auth.Token != MockCommandCenter.TestAppID)
                            {
                                response.StatusCode = (int)HttpStatusCode.BadRequest;
                                response.Close();
                                continue;
                            }
                            break;
                        case AuthorizationType.AzureStorage:
                            CloudStorageAccount cloudStorageAccount;
                            if (!CloudStorageAccount.TryParse(auth.AzureStorageConnectionString, out cloudStorageAccount))
                            {
                                response.StatusCode = (int)HttpStatusCode.BadRequest;
                                response.Close();
                                continue;
                            }
                            break;
                        default:
                            break;
                    }

                    using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    {
                        string eventBatchJson = reader.ReadToEnd();
                        var eventBatch = JsonConvert.DeserializeObject<PartialDecisionServiceMessage>(eventBatchJson);

                        lock (syncLock)
                        {
                            EventBatchList.Add(eventBatch);
                        }

                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.Close();
                    }
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

            lock (syncLock)
            {
                EventBatchList.Clear();
            }

            Interlocked.Exchange(ref requestCount, 0);
        }

        public List<PartialDecisionServiceMessage> EventBatchList { get; set; }

        public int RequestCount { get { return requestCount; } }

        private object syncLock = new object();
        private int requestCount;

        public static readonly string MockJoinServerAddress = "http://localhost:9097/";

        /// <summary>
        /// Using this requires starting local deployment of join service
        /// </summary>
        public static readonly string LocalJoinServerAddress = "http://localhost:1362/";
    }
}
