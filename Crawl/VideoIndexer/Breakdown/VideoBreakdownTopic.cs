using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoBreakdownResultTopic
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
