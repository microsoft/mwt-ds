//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DecisionService.Crawl
{
    public sealed class RSS
    {
        private static HttpClient client = new HttpClient();

        public class URLHolder
        {
            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("param")]
            public string Parameter { get; set; }
        }

        public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
        {
            var url = string.Empty;
            var stopwatch = Stopwatch.StartNew();
            var jsonResponse = string.Empty;

            try
            {
                var reqBodyStr = await req.Content.ReadAsStringAsync();
                var reqBody = JsonConvert.DeserializeObject<URLHolder>(reqBodyStr);

                url = reqBody.Url;
                if (!string.IsNullOrEmpty(reqBody.Parameter))
                    url += reqBody.Parameter;

                log.Info("RSS " + url);

                // TODO: use HttpCachedService (also as means of failover if the RSS stream is down)
                string data = await client.GetStringAsync(reqBody.Url.ToString());
                var rss = XDocument.Parse(data);

                string parseFormat = "ddd, dd MMM yyyy HH:mm:ss zzz";
                string parseFormat2 = "ddd, dd MMM yyyy HH:mm:ss Z";

                var items = rss.DescendantNodes()
                    .OfType<XElement>()
                    .Where(a => a.Name == "item")
                    .Select((elem, index) =>
                    {

                        var pubDateStr = elem.Descendants("pubDate").FirstOrDefault()?.Value;
                        if (pubDateStr != null)
                            pubDateStr = pubDateStr.Trim();

                        DateTime pubDate;
                        if (!DateTime.TryParseExact(pubDateStr, parseFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out pubDate))
                            if (!DateTime.TryParseExact(pubDateStr, parseFormat2, CultureInfo.InvariantCulture, DateTimeStyles.None, out pubDate))
                                pubDate = DateTime.UtcNow;

                        return new { elem, pubDate, index };
                    })
                    .OrderByDescending(elem => elem.pubDate)
                    // limit the feed to avoid getting too many
                    .Take(15)
                    // Note: this is very important for the Dashboard
                    // The order of the items allows customers to specify their base-line policy
                    .OrderBy(elem => elem.index)
                    .Select(x => x.elem);

                var actions = items.Select(x => new
                {
                    ids = new[] { new { id = x.Descendants("link").FirstOrDefault()?.Value } },
                    features = new
                    {
                        _title = x.Descendants("title").FirstOrDefault()?.Value
                    },
                    details = new []
                    {
                        // TODO: properly support 4.2.6.  The "atom:id" Element
                        new { guid = x.Descendants("guid").FirstOrDefault()?.Value }
                    }
                }).ToList();

                jsonResponse = JsonConvert.SerializeObject(actions);

                if (log.Level == TraceLevel.Verbose)
                    log.Trace(new TraceEvent(TraceLevel.Verbose, $"Successfully transformed '{url}' '{data}' to '{jsonResponse}'"));
                else
                    log.Info($"Successfully transformed '{url}'");
            }
            catch (HttpRequestException hre)
            {
                var msg = $"RSS Featurization failed '{url}' for '{req.RequestUri.ToString()}': '{hre.Message}'";
                
                log.Warning(msg);
                // TODO: maybe switch to dependency w/ status failed?
                Services.TelemetryClient.TrackEvent(msg,
                    new Dictionary<string, string>
                    {
                        { "Service", req.RequestUri.ToString() },
                        { "Url", url },
                        { "Exception", hre.Message}
                    });
            }
            catch (Exception ex)
            {
                log.Error($"Failed to process '{url}'", ex);

                Services.TelemetryClient.TrackException(
                    ex,
                    new Dictionary<string, string>
                    {
                        { "Service", req.RequestUri.ToString() },
                        { "Url", url }
                    });
                
                // swallow the error message and return empty. That way we can differentiate between real outages 
                // remote errors
            }
            finally
            {
                Services.TelemetryClient.TrackEvent($"RSS {url}",
                    metrics: new Dictionary<string, double>
                    {
                        { "requestTime", stopwatch.ElapsedMilliseconds }
                    });
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    jsonResponse,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    "application/json")
            };
        }
    }
}
