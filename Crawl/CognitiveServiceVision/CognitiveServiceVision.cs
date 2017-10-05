//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Threading;
using Microsoft.DecisionService.Crawl.Data;

namespace Microsoft.DecisionService.Crawl
{
    public class CognitiveServiceVision
    {
        private static readonly CognitiveService cogService;

        static CognitiveServiceVision()
        {
            cogService = new CognitiveService("CogVision", queryParams: "/analyze?visualFeatures=Categories,Tags,Adult,Faces&details=Celebrities&language=en");
        }

        public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log, CancellationToken cancellationToken)
        {
            return await cogService.InvokeAsync(req, log,
                reqBody => new UrlHolder { Url = reqBody.Image },
                (reqBody, blobContent) =>
                {
                    var visionResponse = JsonConvert.DeserializeObject<VisionResponse>(blobContent.Value);

                    // multiple namespaces
                    blobContent.Output = new JObject();

                    // R,S,T,U
                    if (visionResponse.Tags != null)
                        blobContent.Output.Add(
                            new JProperty("RVisionTags",
                            new JObject(
                                visionResponse.Tags.Select(t => new JProperty(t.Name, t.Confidence)))));

                    if (visionResponse.Adult != null)
                        blobContent.Output.Add(
                            new JProperty("SVisionAdult",
                            JObject.Parse(JsonConvert.SerializeObject(visionResponse.Adult))));

                    if (visionResponse.Categories != null)
                    {
                        // not for now
                        //output.Add(
                        //    new JProperty("TVisionCategories",
                        //    new JObject(
                        //        visionResponse.Categories.Select(t => new JProperty(t.Name, t.Score)))));

                        var celebs =
                            visionResponse.Categories
                                .Where(c => c.Detail != null && c.Detail.Celebrities != null)
                                .SelectMany(c => c.Detail.Celebrities)
                                .GroupBy(c => c.Name)
                                .ToList();

                        if (celebs.Count > 0)
                            blobContent.Output.Add(
                                new JProperty("TVisionCelebrities",
                                new JObject(
                                    celebs.Select(t => new JProperty(t.Key, t.Max(x => x.Confidence))))));
                    }
                },
                cancellationToken);
        }

        public class VisionResponse
        {
            [JsonProperty("categories")]
            public Category[] Categories { get; set; }

            [JsonProperty("adult")]
            public Adult Adult { get; set; }

            [JsonProperty("tags")]
            public Tag[] Tags { get; set; }

            [JsonProperty("faces")]
            public Face[] Faces { get; set; }
        }

        public class Category
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("score")]
            public float Score { get; set; }

            [JsonProperty("detail")]
            public CategoryDetail Detail { get; set; }
        }

        public class CategoryDetail
        {
            [JsonProperty("celebrities")]
            public Celebrity[] Celebrities { get; set; }
        }

        public class Celebrity
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("confidence")]
            public float Confidence { get; set; }
        }

        public class Adult
        {
            [JsonProperty("isAdultContent")]
            public bool IsAdultContent { get; set; }

            [JsonProperty("isRacyContent")]
            public bool IsRacyContent { get; set; }

            [JsonProperty("adultScore")]
            public float AdultScore { get; set; }

            [JsonProperty("racyScore")]
            public float RacyScore { get; set; }
        }

        public class Tag
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("confidence")]
            public float Confidence { get; set; }
        }

        public class Face
        {
            [JsonProperty("age")]
            public int Age { get; set; }

            [JsonProperty("gender")]
            public string Gender { get; set; }
        }
    }
}
