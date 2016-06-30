
namespace WebLoadTesting {
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.WebTesting;
    using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
    using Newtonsoft.Json;
    using System.Linq;
    public class FloatFeaturesWebTest : WebTest {
        
        public FloatFeaturesWebTest() {
            this.PreAuthenticate = true;
            this.Proxy = "default";
        }
        
        public override IEnumerator<WebTestRequest> GetRequestEnumerator() {
            // this.Context.Add("URL", "https://mc-mcdele4eznqtkydvq5s.azurewebsites.net");
            // this.Context.Add("URL", "https://localhost:44365");
            // this.Context.Add("auth", "a34bojp73bq3k");

            var baseURL = (string)this.Context["URL"];
            var auth = (string)this.Context["auth"];

            var contextOptions = new[] { "Young", "Old" };
            var rnd = new Random();
            var expectedAction = rnd.Next(0, 2);
            var age = contextOptions[expectedAction];

            expectedAction++; // actions are 1-based 

            //while (true)
            {
                WebTestRequest request1 = new WebTestRequest($"{baseURL}/API/Policy");
                request1.Method = "POST";
                request1.Headers.Add(new WebTestRequestHeader("Accept", "application/json, text/javascript, */*; q=0.01"));
                request1.Headers.Add(new WebTestRequestHeader("X-Requested-With", "XMLHttpRequest"));
                request1.Headers.Add(new WebTestRequestHeader("auth", auth));
                StringHttpBody request1Body = new StringHttpBody();
                request1Body.ContentType = "application/json; charset=utf-8";
                request1Body.InsertByteOrderMark = false;
                request1Body.BodyString = JsonConvert.SerializeObject(new
                {
                    Features = Enumerable.Range(1, 1000).Select(_ => rnd.NextDouble()).ToArray()
                });
                request1.Body = request1Body;
                request1.ExtractValues += Request1_ExtractValues;
                yield return request1;
                request1 = null;

                var conditionalRule1 = new ProbabilisticConditional
                {
                    SuccessProbability = 0.01f
                };

                //    = new ResponseConditional
                //{
                //    ExpectedAction = expectedAction,
                //    InconsistencyProbability = 0.2f
                //};

                this.BeginCondition(conditionalRule1);

                if (this.ExecuteConditionalRule(conditionalRule1))
                {
                    WebTestRequest request2 = new WebTestRequest($"{baseURL}/API/Reward/");
                    request2.Method = "POST";
                    request2.Headers.Add(new WebTestRequestHeader("Accept", "*/*"));
                    request2.Headers.Add(new WebTestRequestHeader("X-Requested-With", "XMLHttpRequest"));
                    request2.Headers.Add(new WebTestRequestHeader("auth", auth));
                    request2.QueryStringParameters.Add("eventId", "{{EventId}}", false, false);
                    StringHttpBody request2Body = new StringHttpBody();
                    request2Body.ContentType = "application/json; charset=utf-8";
                    request2Body.InsertByteOrderMark = false;
                    request2Body.BodyString = "1"; // reward
                    request2.Body = request2Body;
                    yield return request2;
                    request2 = null;
                }

                this.EndCondition(conditionalRule1);
            }
        }

        private void Request1_ExtractValues(object sender, ExtractionEventArgs e)
        {
            var decision = JsonConvert.DeserializeObject<PolicyDecision>(e.Response.BodyString);
            e.WebTest.Context.Add("EventId", decision.EventId);
            e.WebTest.Context.Add("Action", decision.Action);
        }
    }
}
