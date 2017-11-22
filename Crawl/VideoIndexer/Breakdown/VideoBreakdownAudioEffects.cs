//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResultAudioEffects
    {
        [JsonProperty("audioEffectKey")]
        public string AudioEffectKey { get; set; }

        [JsonProperty("seenDuration")]
        public float SeenDuration { get; set; }

        [JsonProperty("seenDurationRatio")]
        public float SeenDurationRatio { get; set; }
    }
}
