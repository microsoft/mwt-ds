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
using System.Collections.Generic;
using System.Threading;
using Microsoft.DecisionService.Crawl.Data;

namespace Microsoft.DecisionService.Crawl
{
    public class CognitiveServiceEmotion
    {
        private static readonly CognitiveService cogService;

        static CognitiveServiceEmotion()
        {
            cogService = new CognitiveService("CogEmotion");
        }

        public static Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log, CancellationToken cancellationToken)
        {
            return cogService.InvokeAsync(req, log, 
                reqBody => new UrlHolder { Url = reqBody.Image },
                (reqBody, blobContent) =>
                {
                    var visionResponse = JsonConvert.DeserializeObject<Face[]>(blobContent.Value);

                    blobContent.Output = new JObject();

                    if (visionResponse != null)
                    {
                        for (int i = 0; i < visionResponse.Length; i++)
                        {
                            blobContent.Output.Add(
                                new JProperty($"Emotion{i}",
                                new JObject(visionResponse[i].Scores.Select(kv => new JProperty(kv.Key, kv.Value)))));
                        }
                    }
                },
                cancellationToken);
        }

        public class Face
        {
            [JsonProperty("scores")]
            public Dictionary<string, float> Scores { get; set; }
        }
    }
}