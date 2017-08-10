using Crawl.VideoIndexer.Ooyala;
using Ooyala.API;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static Microsoft.DecisionService.Crawl.VideoIndexer;

namespace Microsoft.DecisionService.Crawl
{
    public static class Ooyala
    {
        private static Dictionary<string, string> EmptyDict = new Dictionary<string, string>();

        public static OoyalaVideo GetOoyalaVideo(string assetId, VideoIndexerSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(settings.OoyalaKey) || string.IsNullOrEmpty(settings.OoyalaSecret))
                return null;

            var api = new OoyalaAPI(settings.OoyalaKey, settings.OoyalaSecret);

            var output = new OoyalaVideo();

            var asset = api.get("assets/" + assetId, EmptyDict) as Hashtable;
            output.Description = asset?["description"] as string;

            var metadata = api.get($"assets/{assetId}/metadata", EmptyDict) as Hashtable;
            output.Keywords = (metadata?["keywords"] as string)?.Split(',')?.ToList();

            if (api.get($"assets/{assetId}/streams", EmptyDict) is ArrayList streams)
            {
                var q = from stream in streams.OfType<Hashtable>()
                        let streamInfo = stream["is_source"] as bool?
                        where streamInfo == true
                        select stream;

                var sourceStream = q.FirstOrDefault();
                output.Url = sourceStream?["url"] as string;
            }

            return output;
        }
    }
}