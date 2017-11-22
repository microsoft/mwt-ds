//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResultFace
    {
        [JsonProperty("name")]
        public string Name { get; set; } // // Unknown #1

        [JsonProperty("seenDuration")]
        public float SeenDuration { get; set; }

        [JsonProperty("seenDurationRatio")]
        public float SeenDurationRatio { get; set; }
    }
}
