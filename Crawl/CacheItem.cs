//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;
using System;

namespace Microsoft.DecisionService.Crawl
{
    public sealed class CacheItem
    {
        [JsonProperty("nextRefreshTimestamp")]
        public DateTime NextRefreshTimestamp { get; set; }

        [JsonProperty("input")]
        [JsonConverter(typeof(RawStringConverter))]
        public string Input { get; set; }

        [JsonProperty("output")]
        [JsonConverter(typeof(RawStringConverter))]
        public string Output { get; set; }
    }
}
