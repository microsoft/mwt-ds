using ClientDecisionServiceWebSample.Extensions;
using System.IO;
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

            HostingEnvironment.QueueBackgroundWorkItem(token => {
                DecisionServiceWrapper<string>.Create(
                    appId: "louiemart", 
                    appToken: "c7b77291-f267-43da-8cc3-7df7ec2aeb06", 
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
    }
}