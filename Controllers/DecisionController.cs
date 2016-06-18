using DecisionServicePrivateWeb.Classes;
using DecisionServiceWebAPI;
using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace DecisionServicePrivateWeb.Controllers
{
    public class DecisionPolicyController : Controller
    {
        // POST api/decisionPolicy
        // [Route("policy")]
        [HttpPost]
        [AllowAnonymous]
        public ActionResult Post(int defaultAction = -1) 
        {
            ApiUtil.Authenticate(this.Request);

            var telemetry = new TelemetryClient();
            try
            {
                var client = DecisionServiceClientFactory.AddOrGetExisting();
                var context = ApiUtil.ReadBody(this.Request);
                var eventId = ApiUtil.CreateEventId();
                var action = defaultAction != -1 ? client.ChooseAction(eventId, context, defaultAction) : client.ChooseAction(eventId, context);

                return Json(new PolicyDecision
                {
                    EventId = eventId,
                    Action = action
                });
            }
            catch(Exception e)
            {
                telemetry.TrackException(e);
                throw e;
            }
        }
    }

//    public class DecisionRankerController : ApiBaseController
//    {
//        // POST 
////         [Route("ranker")]
//        [HttpPost]
//        public ActionResult Post(int[] defaultActions)
//        {
//            this.Authenticate();

//            var telemetry = new TelemetryClient();
//            try
//            {
//                var client = DecisionServiceClientFactory.AddOrGetExisting();
//                var context = this.ReadBody();
//                var eventId = this.CreateEventId();
//                var actions = defaultActions != null && defaultActions.Length > 0 ?
//                        client.ChooseRanking(eventId, context, defaultActions) :
//                        client.ChooseRanking(eventId, context);

//                return Json(new RankerDecision
//                {
//                    EventId = eventId,
//                    Actions = actions
//                });
//            }
//            catch (Exception e)
//            {
//                telemetry.TrackException(e);
//                throw e;
//            }
//        }
//    }

    public class PolicyDecision
    {
        public string EventId { get; set; }

        public int Action { get; set; }
    }

    public class RankerDecision
    {
        public string EventId { get; set; }

        public int[] Actions { get; set; }
    }
}

