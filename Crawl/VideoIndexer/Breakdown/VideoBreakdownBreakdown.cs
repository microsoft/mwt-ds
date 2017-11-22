//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResultBreakdown
    {
        [JsonProperty("externalId")]
        public string ExternalId { get; set; }

        [JsonProperty("insights")]
        public VideoBreakdownResultInsights Insight { get; set; }
    }
}
