//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using System.IO;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using System;
using Microsoft.DecisionService.Crawl.Data;
using System.Collections.Generic;
using System.Text;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights;
using Newtonsoft.Json.Linq;

namespace Microsoft.DecisionService.Crawl
{
    public class Crawl
    {
        /// <summary>
        /// In order of trys
        /// </summary>
        private static string[] UserAgents = new[]
        {
            "DSbot/1.0 (+https://ds.microsoft.com/bot.htm)",
            "curl/7.35.0"
        };

        public static async Task<CrawlResponse> DownloadHtml(Uri uri, string userAgent, CrawlRequest reqBody)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = userAgent;

            if (!string.IsNullOrEmpty(reqBody.ETag))
                request.Headers.Add(HttpRequestHeader.IfNoneMatch, reqBody.ETag);


            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    // TODO: look for schema.org
                    var html = await reader.ReadToEndAsync();

                    // TODO: support microsoft:ds_id 
                    return HtmlExtractor.Parse(html, new Uri(reqBody.Url));
                }
            }
        }

        public static async Task<CrawlResponse> DownloadJson(Uri uri, string userAgent, CrawlRequest reqBody)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = userAgent;

            if (!string.IsNullOrEmpty(reqBody.ETag))
                request.Headers.Add(HttpRequestHeader.IfNoneMatch, reqBody.ETag);


            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    return new CrawlResponse
                    {
                        Features = JToken.Parse(await reader.ReadToEndAsync())
                    };
                }
            }
        }

        public static async Task<CrawlResponse> Download(CrawlRequest reqBody)
        {
            Uri uri;
            if (!Uri.TryCreate(reqBody.Url, UriKind.Absolute, out uri))
                return null;

            foreach (var userAgent in UserAgents)
            {
                var headRequest = (HttpWebRequest)WebRequest.Create(uri);
                headRequest.Method = "HEAD";
                headRequest.UserAgent = userAgent;

                try
                {
                    // make sure we only crawl HTML
                    using (var response = (HttpWebResponse)await headRequest.GetResponseAsync())
                    {
                        var contentType = response.GetResponseHeader("Content-Type");

                        CrawlResponse result = null;

                        if (string.IsNullOrWhiteSpace(contentType) || contentType.StartsWith("text/html"))
                            result = await DownloadHtml(uri, userAgent, reqBody);

                        if (contentType.StartsWith("application/json"))
                            result = await DownloadJson(uri, userAgent, reqBody);

                        if (contentType.StartsWith("video/") || contentType.StartsWith("audio/"))
                            result = new CrawlResponse { Video = reqBody.Url };

                        if (contentType.StartsWith("image/"))
                            result = new CrawlResponse { Image = reqBody.Url };

                        return result;
                    }
                }
                catch (WebException we)
                {
                    HttpWebResponse httpResponse = we.Response as HttpWebResponse;
                    if (we.Status == WebExceptionStatus.ServerProtocolViolation)
                    {
                        // Get a little more telemetry about what is going on here, though most cases don't
                        // have a Response object.
                        IDictionary<string, string> traceData = new Dictionary<string, string>()
                        {
                            { "HasResponse", (we.Response != null).ToString() }
                        };

                        if (we.Response != null)
                        {
                            traceData["Response.SupportsHeaders"] = we.Response.SupportsHeaders.ToString();

                            if (we.Response.SupportsHeaders)
                            {
                                for (int i = 0; i < we.Response.Headers.Count; i++)
                                {
                                    string headerName = we.Response.Headers.GetKey(i);
                                    string headerValue = we.Response.Headers.Get(i);
                                    traceData[$"Response.Headers.{headerName}"] = headerValue;
                                }
                            }

                            if (httpResponse != null)
                            {
                                traceData["HttpResponse.StatusCode"] = httpResponse.StatusCode.ToString();
                            }
                        }

                        Services.TelemetryClient.TrackTrace($"Download target ({uri}) ServerProtocolViolation", SeverityLevel.Error, traceData);

                        // Ignore known cases where crawl fails due to error on the crawl-target side - these should not
                        // cause a hard failure on our end.
                        continue;
                    }

                    if (httpResponse != null)
                    {
                        // Ignore known cases where crawl fails due to error on the crawl-target side - these should not
                        // cause a hard failure on our end.
                        if (httpResponse.StatusCode == HttpStatusCode.Forbidden ||
                            httpResponse.StatusCode == HttpStatusCode.NotFound ||
                            httpResponse.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            continue;
                        }
                    }

                    throw;
                }
            }

            throw new UnauthorizedAccessException("Unable to access HTTP endpoint");
        }

        public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
        {
            CrawlRequest crawlRequest = null;
            string reqBodyStr = null;
            try
            {
                using (var operation = Services.TelemetryClient.StartOperation<DependencyTelemetry>("Crawl.HTML"))
                {
                    reqBodyStr = await req.Content.ReadAsStringAsync();
                    var reqBody = JsonConvert.DeserializeObject<CrawlRequest>(reqBodyStr);

                    operation.Telemetry.Properties.Add("AppId", reqBody.Site);
                    operation.Telemetry.Properties.Add("ActionId", reqBody.Id);
                    operation.Telemetry.Properties.Add("Url", reqBody.Url);

                    log.Info($"Crawl AppId={reqBody.Site} Id={reqBody.Id} Url={reqBody.Url}");

                    var crawlResponse = await Download(reqBody);

                    // always return a valid object so that downstream workflows can continue
                    if (crawlResponse == null)
                        crawlResponse = new CrawlResponse();

                    crawlResponse.Url = reqBody.Url;
                    crawlResponse.Site = reqBody.Site;
                    crawlResponse.Id = reqBody.Id;

                    var json = JsonConvert.SerializeObject(crawlResponse, new JsonSerializerSettings
                    {
                        Formatting = Formatting.None,
                        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
                    });

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            json,
                            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                            "application/json")
                    };
                }
            }
            catch (Exception ex)
            {
                var props = new Dictionary<string, string>
                    {
                        { "Service", req.RequestUri.ToString() }
                };

                if (crawlRequest == null)
                    props.Add("JSON", reqBodyStr);
                else
                {
                    props.Add("Url", crawlRequest.Url);
                    props.Add("AppId", crawlRequest.Site);
                    props.Add("ActionId", crawlRequest.Id);
                }

                Services.TelemetryClient.TrackException(ex, props);

                throw ex;
            }
        }
    }
}
