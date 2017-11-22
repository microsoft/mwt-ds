//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResultSummarizedInsights
    {
        [JsonProperty("faces")]
        public VideoBreakdownResultFace[] Faces { get; set; }

        [JsonProperty("topics")]
        public VideoBreakdownResultTopic[] Topics { get; set; }

        [JsonProperty("sentiments")]
        public VideoBreakdownResultSentiment[] Sentiments { get; set; }
    }
}
