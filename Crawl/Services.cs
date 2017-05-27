//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Crawl.Data;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web;

namespace Microsoft.DecisionService.Crawl
{
    public static class Services
    {
        public readonly static TelemetryClient TelemetryClient;

        static Services()
        {
            TelemetryConfiguration.Active.InstrumentationKey = System.Configuration.ConfigurationManager.AppSettings["AppInsightsKey"];
            TelemetryClient = new TelemetryClient();
            TelemetryClient.Context.Cloud.RoleName = "Crawl";
            TelemetryClient.Context.Component.Version = typeof(Services).Assembly.GetName().Version.ToString();
        }

        public static string Limit(string text, int numBytes)
        {
            if (Encoding.UTF8.GetByteCount(text) < numBytes)
                return text;

            var chars = text.ToCharArray();
            var length = Math.Min(text.Length, numBytes);

            while (Encoding.UTF8.GetByteCount(chars, 0, length) > numBytes)
                length--;

            return text.Substring(length);
        }

        public static HttpResponseMessage CreateResponse(this HttpRequestMessage req, BlobContent blobContent)
        {
            blobContent.Output?.Add(new JProperty("_expires", blobContent.Expires));

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    blobContent.Output?.ToString(Formatting.None) ?? string.Empty,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    "application/json")
            };

            // Get replaced in deployed version
            // response.Content.Headers.Expires = expires;

            // response.Content.Headers.TryAddWithoutValidation("X-DecisionService-Expires", expires.ToString("ddd, dd MMM yyyy HH:mm:ss 'GMT'"));

            return response;
        }

        public static void TrackException(Exception ex, HttpRequestMessage req, TraceWriter log, string reqBodyStr, CrawlResponse reqBody, BlobContent blobContent)
        {
            var props = new Dictionary<string, string>
            {
                { "Service", req.RequestUri.ToString() },
                { "Request", reqBodyStr }
            };

            if (reqBody != null)
            {
                props.Add("AppId", reqBody.Site);
                props.Add("ActionId", reqBody.Id);
            }

            if (blobContent != null)
                props.Add("Response", blobContent.Value);

            TelemetryClient.TrackException(ex, props);
            log.Error($"Request for AppId={reqBody?.Site} ActionId={reqBody?.Id} failed", ex);
        }
    }
}
