//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResultInsights
    {
        [JsonProperty("contentModeration")]
        public VideoBreakdownResultContentModeration ContentModeration { get; set; }

        [JsonProperty("viewToken")]
        public string ViewToken { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("transcriptBlocks")]
        public VideoBreakdownResultTranscriptBlock[] TranscriptBlocks { get; set; }
    }
}
