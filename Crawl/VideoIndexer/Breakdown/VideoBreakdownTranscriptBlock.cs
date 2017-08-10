using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResultTranscriptBlock
    {
        [JsonProperty("lines")]
        public VideoBreakdownResultTranscriptBlockLine[] Lines { get; set; }

        [JsonProperty("sentiment")]
        public float Sentiment { get; set; }
    }
}
