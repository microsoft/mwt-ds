//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResultSentiment
    {
        [JsonProperty("sentimentKey")]
        public string SentimentKey { get; set; }

        [JsonProperty("seenDurationRatio")]
        public float SeenDurationRatio { get; set; }
    }
}
