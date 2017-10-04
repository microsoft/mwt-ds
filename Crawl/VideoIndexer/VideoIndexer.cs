//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.DecisionService.Crawl.Data;
using Newtonsoft.Json.Linq;
using System.Linq;
using System;
using System.Threading;
using Microsoft.WindowsAzure.Storage;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights;
using Crawl.VideoIndexer;
using System.Web;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoIndexer
    {
        private static readonly CognitiveService cogService;

        public class VideoIndexerSettings
        {
            [JsonProperty("key")]
            public string VideoIndexerKey { get; set; }

            [JsonProperty("ooyalaKey")]
            public string OoyalaKey { get; set; }

            [JsonProperty("ooyalaSecret")]
            public string OoyalaSecret { get; set; }
        }

        static VideoIndexer()
        {
            cogService = new CognitiveService("VideoIndexer");
        }

        private static async Task<VideoIndexerSettings> GetVideoIndexerSettings(string appId)
        {
            var account = CloudStorageAccount.Parse(await cogService.GetAzureStorageConnectionStringAsync());
            var blobClient = account.CreateCloudBlobClient();

            var keyContainer = blobClient.GetContainerReference("keys");
            if (!await keyContainer.ExistsAsync())
                return null;

            var videoIndexerConfig = keyContainer.GetBlockBlobReference($"videoindexer.{appId}.json");
            if (!videoIndexerConfig.Exists())
                return null;

            return JsonConvert.DeserializeObject<VideoIndexerSettings>(await videoIndexerConfig.DownloadTextAsync());
        }

        private static CognitiveService GetCognitiveService(VideoIndexerSettings settings)
        {
            var localCogService = cogService;
            var videoIndexerKey = settings?.VideoIndexerKey;
            if (!string.IsNullOrEmpty(videoIndexerKey))
                localCogService = new CognitiveService("VideoIndexer", apiKey: videoIndexerKey);

            return localCogService;
        }

        private static async Task<BlobContent> GetVideoIndexerBreakdownAsync(CrawlResponse reqBody, VideoIndexerSettings settings, TraceWriter log, CancellationToken cancellationToken)
        {
            using (var operation = Services.TelemetryClient.StartOperation<DependencyTelemetry>("Crawl.VideoIndexer.GetBreakdown"))
            {
                var localCogService = GetCognitiveService(settings);

                var client = await localCogService.GetHttpClientAsync();

                var id = HttpUtility.UrlEncode(reqBody.Id);
                var responseMessage = await client.GetAsync($"/Breakdowns/Api/Partner/Breakdowns/Search?externalId={id}", cancellationToken);
                if (!responseMessage.IsSuccessStatusCode)
                    return null;

                var videoIndexerResponseStr = await responseMessage.Content.ReadAsStringAsync();
                var videoIndexerResponse = JsonConvert.DeserializeObject<VideoIndexerSearchResult>(videoIndexerResponseStr);
                var breakdownId = videoIndexerResponse.Results?.FirstOrDefault()?.Id;
                if (breakdownId == null)
                    return null;

                return await localCogService.RequestAsync(
                    log,
                    reqBody.Site,
                    reqBody.Id,
                    $"/Breakdowns/Api/Partner/Breakdowns/{breakdownId}",
                    reqBody.ForceRefresh,
                    isPost: false,
                    cancellationToken: cancellationToken);
            }
        }

        private static async Task IndexVideo(CrawlResponse reqBody, VideoIndexerSettings settings)
        {
            if (reqBody == null || string.IsNullOrEmpty(reqBody.Video) || !Uri.TryCreate(reqBody.Video, UriKind.Absolute, out Uri unused))
                return;

            //var account = CloudStorageAccount.Parse(await cogService.GetAzureStorageConnectionStringAsync());
            //var blobClient = account.CreateCloudBlobClient();

            //var container = blobClient.GetContainerReference(BlobCache.ToContainerName(DateTime.UtcNow, cogService.containerName));
            //await container.CreateIfNotExistsAsync();

            //var leaseBlob = container.GetBlockBlobReference(BlobCache.ToBlobName(reqBody.Site, reqBody.Id) + ".lease");
            //leaseBlob.

            using (var operation = Services.TelemetryClient.StartOperation<DependencyTelemetry>("Crawl.VideoIndexer.Enqueue"))
            {
                operation.Telemetry.Properties.Add("url", reqBody.Video);
                operation.Telemetry.Properties.Add("id", reqBody.Id);

                // https://videobreakdown.azure-api.net/Breakdowns/Api/Partner/Breakdowns[?name][&privacy][&videoUrl][&language][&externalId][&metadata][&description][&partition][&callbackUrl][&indexingPreset][&streamingPreset]
                var url = HttpUtility.UrlEncode(reqBody.Video);
                var id = HttpUtility.UrlEncode(reqBody.Id);
                var query =
                    "Breakdowns/Api/Partner/Breakdowns" +
                    $"?externalId={id}" +
                    $"&videoUrl={url}" +
                    "&privacy=private&searchable=true";

                string name = reqBody.Title;
                if (string.IsNullOrEmpty(name))
                    name = id;

                query += "&name=" + name;

                if (!string.IsNullOrEmpty(reqBody.Description))
                    query += "&description=" + reqBody.Description;

                if (reqBody.Categories != null && reqBody.Categories.Count > 0)
                    query += "&metadata=" + HttpUtility.UrlEncode(string.Join(" ", reqBody.Categories));

                var localCogService = GetCognitiveService(settings);
                
                var client = await localCogService.GetHttpClientAsync();
               

                var httpResponse = await client.PostAsync(client.BaseAddress + query, new MultipartFormDataContent());

                operation.Telemetry.Success = httpResponse.IsSuccessStatusCode;
                operation.Telemetry.ResultCode = httpResponse.StatusCode.ToString();
            }
        }

        public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log, CancellationToken cancellationToken)
        {
            string reqBodyStr = null;
            CrawlResponse reqBody = null;

            try
            {
                using (var operation = Services.TelemetryClient.StartOperation<DependencyTelemetry>("Crawl.VideoIndexer"))
                {
                    // TODO: if the id is not parsable, just ignore - make sure the others do too

                    reqBodyStr = await req.Content.ReadAsStringAsync();
                    reqBody = JsonConvert.DeserializeObject<CrawlResponse>(reqBodyStr);

                    operation.Telemetry.Properties.Add("AppId", reqBody.Site);
                    operation.Telemetry.Properties.Add("ActionId", reqBody.Id);

                    if (string.IsNullOrEmpty(reqBody.Id))
                        return Services.CreateResponse(new BlobContent { Expires = DateTime.UtcNow + TimeSpan.FromMinutes(5) });

                    var settings = await GetVideoIndexerSettings(reqBody.Site);

                    // find existing breakdown
                    var breakdownContent = await GetVideoIndexerBreakdownAsync(reqBody, settings, log, cancellationToken);

                    // The GetVideoIndexerBreakdownAsync call can, if the underlying call to VideoIndexer GetBreakdown fails - but the 
                    // call to VideoIndexer SearchBreakdown succeeds, return a BlobContent with an empty Value, and a short "expires".
                    // Treat that as the same as not getting a VideoIndexer response.
                    if (breakdownContent == null || string.IsNullOrWhiteSpace(breakdownContent.Value))
                    {
                        if (string.IsNullOrEmpty(reqBody.Video))
                        {
                            // try to resolve video through Ooyala
                            var ooyalaVideo = Ooyala.GetOoyalaVideo(reqBody.Id, settings);

                            if (ooyalaVideo != null)
                            {
                                reqBody.Video = ooyalaVideo.Url;

                                if (string.IsNullOrEmpty(reqBody.Title))
                                    reqBody.Title = ooyalaVideo.Title;

                                if (string.IsNullOrEmpty(reqBody.Description))
                                    reqBody.Description = ooyalaVideo.Description;

                                if (reqBody.Categories == null || reqBody.Categories.Count == 0)
                                    reqBody.Categories = ooyalaVideo.Keywords;
                            }
                        }

                        // enqueue break down indexing
                        await IndexVideo(reqBody, settings);

                        // make sure caller comes back in 5min
                        return Services.CreateResponse(new BlobContent { Expires = DateTime.UtcNow + TimeSpan.FromMinutes(5) });
                    }

                    var result = JsonConvert.DeserializeObject<VideoBreakdownResult>(breakdownContent.Value);
                    if (result.State != "Processed")
                        // make sure caller comes back in 5min
                        return Services.CreateResponse(new BlobContent { Expires = DateTime.UtcNow + TimeSpan.FromMinutes(5) });
                    

                    // featurize
                    breakdownContent.Output = VideoIndexerFeaturizer.FeaturizeVideoIndexerBreakdown(result);

                    return Services.CreateResponse(breakdownContent);
                }
            }
            catch (Exception ex)
            {
                Services.TrackException(ex, req, log, reqBodyStr, reqBody);
                throw ex;
            }
        }
    }
}
