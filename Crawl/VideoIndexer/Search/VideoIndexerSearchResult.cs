using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    public class VideoIndexerSearchResult
    {
        [JsonProperty("results")]
        public VideoIndexerSearchResultItem[] Results { get; set; }
    }
}
