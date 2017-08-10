using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResult
    {
        [JsonProperty("state")] // processed
        public string State { get; set; }

        [JsonProperty("durationInSeconds")]
        public int DurationInSeconds { get; set; }

        [JsonProperty("breakdowns")]
        public VideoBreakdownResultBreakdown[] Breakdowns { get; set; }


        [JsonProperty("summarizedInsights")]
        public VideoBreakdownResultSummarizedInsights SummarizedInsights { get; set; }
    }
}
