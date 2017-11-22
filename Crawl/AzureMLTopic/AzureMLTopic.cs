using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.DecisionService.Crawl.Data;
using Newtonsoft.Json.Linq;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http.Headers;
using System.Threading;

namespace Microsoft.DecisionService.Crawl
{
    public class AzureMLTopic
    {
        private static readonly HttpCachedService cachedService;

        static AzureMLTopic()
        {
            cachedService = new HttpCachedService("AzureMLTopic");
            cachedService.client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cachedService.apiKey);
        }

        public static Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log, CancellationToken cancellationToken)
        {
            return cachedService.InvokeAsync(req, log, 
                reqBody =>
                {
                    var scoreRequest = new
                    {
                        Inputs = new Dictionary<string, StringTable>(),
                        GlobalParameters = new Dictionary<string, string>() { }
                    };

                    scoreRequest.Inputs.Add("input1", new StringTable
                    {
                        ColumnNames = new string[] { "Text" },
                        Values = new string[,] { { reqBody.Article } }
                    });

                    return scoreRequest;
                },
                (reqBody, blobContent) =>
                {
                    blobContent.Output = new JObject();

                    var jobj = JObject.Parse(blobContent.Value);
                    var topicRemoteRaw = jobj.SelectToken("$.Results.output1.value.Values[0][0]");
                    if (topicRemoteRaw != null)
                        blobContent.Output.Add(new JProperty("topics", topicRemoteRaw.Value<string>().Split(',').Select(float.Parse).ToArray()));
                },
                isPost: true,
                cancellationToken: cancellationToken);
        }

        public class StringTable
        {
            public string[] ColumnNames { get; set; }

            public string[,] Values { get; set; }
        }
    }
}
