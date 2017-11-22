//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResultTranscriptBlockLine
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("sentiment")]
        public float Confidence { get; set; }
    }
}