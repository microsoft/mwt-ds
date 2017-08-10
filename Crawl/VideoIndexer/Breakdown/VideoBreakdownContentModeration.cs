using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResultContentModeration
    {
        [JsonProperty("adultClassifierValue")]
        public float AdultClassifierValue { get; set; }

        [JsonProperty("bannedWordsCount")]
        public int BannedWordsCount { get; set; }

        [JsonProperty("isSuspectedAsAdult")]
        public bool IsSuspectedAsAdult { get; set; }

        [JsonProperty("isAdult")]
        public bool IsAdult { get; set; }
    }
}
