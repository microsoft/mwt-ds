//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.DecisionService.Crawl.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace Microsoft.DecisionService.Crawl
{
    public class HttpCachedService
    {
        internal readonly string containerName;
        internal HttpClient client;
        internal string endpoint;
        internal string apiKey;
        internal string storageConnectionString;

        public HttpCachedService(string containerName)
        {
            // limit due to Azure Storage container name
            if (containerName.Length > 24 - 6 /* yyyyMM */)
                throw new ArgumentException($"{nameof(containerName)}: '{containerName}' is too long. Must be {24 - 6} characters at most.");
            this.containerName = containerName;
        }

        protected virtual void Initialize()
        { }

        private async Task InitializeAsync()
        {
            if (this.client != null)
                return;

            var keyVaultUrl = ConfigurationManager.AppSettings["KeyVaultUrl"];

            if (!string.IsNullOrEmpty(keyVaultUrl))
            {
                var keyVaultHelper = new KeyVaultHelper(
                    StoreLocation.CurrentUser,
                    ConfigurationManager.AppSettings["AzureActiveDirectoryClientId"],
                    ConfigurationManager.AppSettings["AzureActiveDirectoryCertificateThumbprint"]);

                var keyVault = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(keyVaultHelper.GetAccessToken));

                this.endpoint = (await keyVault.GetSecretAsync(keyVaultUrl, containerName + "Endpoint").ConfigureAwait(false)).Value;
                this.apiKey = (await keyVault.GetSecretAsync(keyVaultUrl, containerName + "Key").ConfigureAwait(false)).Value;
                this.storageConnectionString = (await keyVault.GetSecretAsync(keyVaultUrl, "StorageConnectionString").ConfigureAwait(false)).Value;
            }
            else
            {
                // fallback to local settings
                this.endpoint = ConfigurationManager.AppSettings[containerName + "Endpoint"];
                this.apiKey = ConfigurationManager.AppSettings[containerName + "Key"];
                this.storageConnectionString = ConfigurationManager.AppSettings["StorageConnectionString"];
            }

            this.client = new HttpClient()
            {
                BaseAddress = new Uri(this.endpoint)
            };

            this.Initialize();
        }

        public async Task<BlobContent> PostAsync(TraceWriter log, string site, string id, object request, bool forceRefresh, CancellationToken cancellationToken)
        {
            await this.InitializeAsync();

            var stopwatch = Stopwatch.StartNew();
            var cacheHit = true;
            HttpResponseMessage responseMessage = null;
            string body = null;

            try
            {
                body = request as string;
                string input;
                string contentType;
                if (body != null)
                {
                    // if this is a raw string, we need to escape for storage
                    input = JsonConvert.SerializeObject(request);
                    contentType = "text/plain";
                }
                else
                {
                    body = JsonConvert.SerializeObject(request);
                    input = body;
                    contentType = "application/json";
                }

                log.Trace(new TraceEvent(TraceLevel.Verbose,
                    $"Requesting {this.containerName} at {this.endpoint}: {body}"));

                var blobCache = new BlobCache(this.storageConnectionString);

                // lookup Azure Blob storage cache first
                // have a 5min timeout for retries
                BlobContent blobContent = null;
                if (!forceRefresh)
                    blobContent = await blobCache.GetAsync(site, id, this.containerName, input, TimeSpan.FromMinutes(5), cancellationToken);

                if (blobContent == null)
                {
                    cacheHit = false;

                    var stopwatchReqeust = Stopwatch.StartNew();

                    // make the actual HTTP request
                    responseMessage = await this.client.PostAsync(
                        string.Empty,
                        new StringContent(
                            body,
                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                            contentType));

                    Services.TelemetryClient.TrackDependency(this.containerName, this.endpoint, this.containerName, null,
                        DateTime.UtcNow, stopwatchReqeust.Elapsed,
                        $"{responseMessage.StatusCode} {responseMessage.ReasonPhrase}", responseMessage.IsSuccessStatusCode);

                    log.Trace(new TraceEvent(TraceLevel.Verbose, $"Response: {responseMessage.StatusCode} {responseMessage.ReasonPhrase}"));

                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        blobContent = new BlobContent
                        {
                            // TODO: random expiration
                            Expires = DateTime.UtcNow + TimeSpan.FromMinutes(1),
                        };
                    }
                    else
                    {
                        var responseStr = await responseMessage.Content.ReadAsStringAsync();

                        log.Trace(new TraceEvent(TraceLevel.Verbose, $"Result {this.containerName} at {this.endpoint}: {responseStr}"));

                        // once we got a response, cache for 3 days
                        // TODO: add configuration option
                        // TODO: add force refresh parameter
                        blobContent = await blobCache.PersistAsync(site, id, this.containerName, input, responseStr, TimeSpan.FromDays(3), cancellationToken);
                    }
                }

                return blobContent;
            }
            finally
            {
                var props = new Dictionary<string, string>
                    {
                        { "site", site },
                        { "id", id },
                        { "cacheHit", cacheHit.ToString() },
                        { "StatusCode", responseMessage?.StatusCode.ToString() },
                        { "Reason", responseMessage?.ReasonPhrase }
                    };

                var sb = new StringBuilder(this.containerName);
                if (responseMessage != null && responseMessage.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    props.Add("Request", body);
                    sb.Append(" failed");
                }

                Services.TelemetryClient.TrackEvent(
                    sb.ToString(),
                    props,
                    metrics: new Dictionary<string, double>
                    {
                        { "requestTime", stopwatch.ElapsedMilliseconds }
                    });
            }
        }

        public async Task<HttpResponseMessage> InvokeAsync(HttpRequestMessage req, TraceWriter log,
            Func<CrawlResponse, object> requestBodyFunc,
            Action<CrawlResponse, BlobContent> responseAction,
            CancellationToken cancellationToken)
        {
            log.Info("Crawl." + this.containerName);

            await this.InitializeAsync();

            string reqBodyStr = null;
            CrawlResponse reqBody = null;
            BlobContent blobContent = null;

            try
            {
                using (var operation = Services.TelemetryClient.StartOperation<DependencyTelemetry>("Crawl." + this.containerName))
                {
                    reqBodyStr = await req.Content.ReadAsStringAsync();
                    reqBody = JsonConvert.DeserializeObject<CrawlResponse>(reqBodyStr);

                    operation.Telemetry.Target = this.endpoint;
                    operation.Telemetry.Properties.Add("AppId", reqBody.Site);
                    operation.Telemetry.Properties.Add("ActionId", reqBody.Id);

                    blobContent = await this.PostAsync(
                        log,
                        reqBody.Site,
                        reqBody.Id,
                        requestBodyFunc(reqBody),
                        reqBody.ForceRefresh,
                        cancellationToken);

                    if (blobContent != null)
                    {
                        operation.Telemetry.Properties.Add("Expires", blobContent.Expires.ToString(CultureInfo.InvariantCulture));

                        if (blobContent.Value != null)
                        {
                            responseAction(reqBody, blobContent);

                            operation.Telemetry.ResultCode = "OK";
                        }
                    }

                    return req.CreateResponse(blobContent);
                }
            }
            catch (Exception ex)
            {
                Services.TrackException(ex, req, log, reqBodyStr, reqBody, blobContent);
                throw ex;
            }
        }
    }
}
