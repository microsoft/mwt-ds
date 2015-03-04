using ClientDecisionServiceWebSample.Extensions;
using System.Web.Hosting;
using System.Web.Mvc;

namespace ClientDecisionServiceWebSample.Controllers
{
    public class HomeController : AsyncController
    {
        static int requestCount = 0;
        static readonly string exploreFile = HostingEnvironment.MapPath("~/dsexplore.txt");

        public ActionResult Index()
        {
            if (requestCount == 0)
            {
                System.IO.File.Delete(exploreFile);
            }
            requestCount++;

            HostingEnvironment.QueueBackgroundWorkItem(token => {
                DecisionServiceWrapper<string>.Create(HostingEnvironment.MapPath("~/"));

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