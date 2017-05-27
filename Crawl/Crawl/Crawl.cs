//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using System.IO;
using System.Net;
using System.Net.Http;
using Crawl.Crawl;
using Newtonsoft.Json;
using System;
using Microsoft.DecisionService.Crawl.Data;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights;

namespace Microsoft.DecisionService.Crawl
{
    public class Crawl
    {
        // <meta property="microsoft:ds_id" content="some-id">
        //private static Regex MetaMicrosoftDsIdRegex = new Regex(@"<meta[^>]+property\s*=\s*[""']microsoft:ds_id[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        //private static Regex MetaContentRegex = new Regex(@"content\s*=\s*[""']([^""']+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

                    var request = (HttpWebRequest)WebRequest.Create(reqBody.Url);

                    if (!string.IsNullOrEmpty(reqBody.ETag))
                        request.Headers.Add(HttpRequestHeader.IfNoneMatch, reqBody.ETag);

                    request.Method = "GET";
                    request.KeepAlive = true;
                    request.UserAgent = "DSbot/1.0 (+https://ds.microsoft.com/bot.htm)";

                    using (var response = (HttpWebResponse)await request.GetResponseAsync())
                    {
                        operation.Telemetry.ResultCode = response.StatusCode.ToString();

                        using (var stream = response.GetResponseStream())
                        using (var reader = new StreamReader(stream))
                        {
                            // TODO: allow direct JSON
                            // TODO: look for schema.org
                            var html = await reader.ReadToEndAsync();

                            // TODO: support microsoft:ds_id 
                            var result = HtmlExtractor.Parse(html, new Uri(reqBody.Url));
                            result.Url = reqBody.Url;
                            result.Site = reqBody.Site;
                            result.Id = reqBody.Id;

                            return new HttpResponseMessage(HttpStatusCode.OK)
                            {
                                Content = new StringContent(
                                    JsonConvert.SerializeObject(result, new JsonSerializerSettings
                                    {
                                        Formatting = Formatting.None,
                                        StringEscapeHandling = StringEscapeHandling.EscapeNonAscii
                                    }),
                                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                                    "application/json")
                            };
                        }
                    }
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
