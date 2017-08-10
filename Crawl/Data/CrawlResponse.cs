//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Microsoft.DecisionService.Crawl.Data
{
    public class CrawlResponse
    {
        [JsonProperty("site")]
        public string Site { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }

        [JsonProperty("categories", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Categories { get; set; }

        [JsonProperty("image", NullValueHandling = NullValueHandling.Ignore)]
        public string Image { get; set; }

        [JsonProperty("article", NullValueHandling = NullValueHandling.Ignore)]
        public string Article { get; set; }

        [JsonProperty("ds_id", NullValueHandling = NullValueHandling.Ignore)]
        public string PassThroughDetails { get; set; }

        [JsonProperty("forceRefresh")]
        public bool ForceRefresh { get; set; } = false;

        [JsonProperty("video", NullValueHandling = NullValueHandling.Ignore)]
        public string Video { get; set; }
    }
}
