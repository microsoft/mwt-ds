using System.Collections.Generic;

namespace Crawl.VideoIndexer.Ooyala
{
    public class OoyalaVideo
    {
        public string Url { get; set; }

        public List<string> Keywords { get; set; }

        public string Description { get; set; }

        public string Title { get; set; }
    }
}