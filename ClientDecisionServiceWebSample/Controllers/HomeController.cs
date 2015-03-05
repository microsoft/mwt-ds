using ClientDecisionServiceWebSample.Extensions;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Web.Hosting;
using System.Web.Mvc;

namespace ClientDecisionServiceWebSample.Controllers
{
    public class HomeController : AsyncController
    {
        static int requestCount = 0;
        static readonly string outputDir = HostingEnvironment.MapPath("~/Output");
        static readonly string exploreFile = Path.Combine(outputDir, "dsexplore.txt");

        public ActionResult Index()
        {
            Directory.CreateDirectory(outputDir);
            if (requestCount == 0)
            {
                System.IO.File.Delete(exploreFile);
            }
            requestCount++;

            string appToken = "c7b77291-f267-43da-8cc3-7df7ec2aeb06";

            HostingEnvironment.QueueBackgroundWorkItem(cancelToken =>
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    cancelToken.WaitHandle.WaitOne(5000);
                    HomeController.RetrainModel(appToken, numberOfActions: 2);
                }
            });

            HostingEnvironment.QueueBackgroundWorkItem(cancelToken => {
                DecisionServiceWrapper<string>.Create(
                    appId: "louiemart",
                    appToken: appToken, 
                    epsilon: .2f, 
                    numActions: 10,
                    modelOutputDir: outputDir);

                string context = "context " + requestCount;
                string outcome = "outcome " + requestCount;

                uint action = DecisionServiceWrapper<string>.Service.ChooseAction(requestCount.ToString(), context);
                DecisionServiceWrapper<string>.Service.ReportOutcome(outcome, requestCount.ToString());

                System.IO.File.AppendAllLines(exploreFile, new string[] { "Action: " + action.ToString() });
            });

            string explorationData = System.IO.File.Exists(exploreFile) ? string.Join("<br />", System.IO.File.ReadAllLines(exploreFile)) : string.Empty;

            return Content(string.Format("Ok. Request: {0}. Exploration: {1}", requestCount, explorationData));
        }

        static void RetrainModel(string appToken, int numberOfActions)
        {
            using (var client = new HttpClient())
            {
                var values = new Dictionary<string, string>
                {
                    { "token", appToken },
                    { "numberOfActions", numberOfActions.ToString() }
                };

                var content = new FormUrlEncodedContent(values);

                var responseTask = client.PostAsync(
                    "http://mwtds.azurewebsites.net//Application/RetrainModel",
                    content
                );
                responseTask.Wait();

                var response = responseTask.Result;

                if (!response.IsSuccessStatusCode)
                {
                    var t2 = response.Content.ReadAsStringAsync();
                    t2.Wait();
                    Trace.TraceError("Failed to retrain model.");
                    Trace.WriteLine(t2.Result);
                    Trace.WriteLine(response.ReasonPhrase);
                    Trace.WriteLine(response.Headers.ToString());
                }
                else
                {
                    Trace.TraceInformation("Retraining model success.");
                }
            }
        }
    }
}