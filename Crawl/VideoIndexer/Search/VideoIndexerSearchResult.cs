//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoIndexerSearchResult
    {
        [JsonProperty("results")]
        public VideoIndexerSearchResultItem[] Results { get; set; }
    }
}