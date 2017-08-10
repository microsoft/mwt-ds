using Newtonsoft.Json;

namespace Microsoft.DecisionService.Crawl
{
    /// <summary>
    /// There are more fields here
    /// </summary>
    public class VideoIndexerSearchResultItem
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }
}
