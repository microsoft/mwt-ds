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
