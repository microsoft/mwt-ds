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
using System.Text;
using System.Threading;

namespace Microsoft.DecisionService.Crawl
{
    public class CognitiveServiceEntityLinking
    {
        private static readonly CognitiveService cogService;

        static CognitiveServiceEntityLinking()
        {
            cogService = new CognitiveService("CogEntityLinking", queryParams: "/entities");
        }

        public static Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log, CancellationToken cancellationToken)
        {
            return cogService.InvokeAsync(req, log,
                reqBody =>
                {
                    var textBuilder = new StringBuilder();

                    if (!string.IsNullOrEmpty(reqBody.Title))
                        textBuilder.AppendLine(reqBody.Title);
                    if (!string.IsNullOrEmpty(reqBody.Article))
                        textBuilder.AppendLine(reqBody.Article);

                    if (textBuilder.Length == 0)
                        return null;

                    string text = Services.Limit(textBuilder.ToString(), 10240);

                    // EntityLinking has moved under the TextAnalytics Cognitive Service, so the request body is now shaped
                    // like the TextAnalytics request
                    return CognitiveServiceTextAnalytics.CreateRequestFromText(text);
                },
                (reqBody, blobContent) =>
                {
                    blobContent.Output = new JObject();
                    var responseObj = JsonConvert.DeserializeObject<TextAnalyticResponse<DocumentSentiment>>(blobContent.Value);
                    if (responseObj?.Documents?.Length == 1)
                    {
                        var entityResponse = responseObj.Documents[0];

                        if (entityResponse?.Entities != null)
                        {
                            var q = entityResponse.Entities
                                .GroupBy(e => e.Name)
                                .Select(e => new JProperty(e.Key, e.Max(x => x.Score)));

                            blobContent.Output.Add("Tags", new JObject(q));
                        }
                    }
                },
                isPost: true,
                cancellationToken: cancellationToken);
        }

        public class DocumentEntities : TextAnalyticDocumentResultBase
        {
            [JsonProperty("entities")]
            public Entity[] Entities { get; set; }
        }

        public class Entity
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("score")]
            public float Score { get; set; }
        }
    }
}
