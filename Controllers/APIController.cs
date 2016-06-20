using DecisionServicePrivateWeb.Classes;
using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using VW;
using VW.Serializer;

namespace DecisionServicePrivateWeb.Controllers
{
    [RequireHttps]
    public class APIController : Controller
    {
        private ApplicationClientMetadata metaData;
        private DateTime lastDownload;

        private const string AuthHeaderName = "auth";

        [HttpPost]
        public ActionResult Validate()
        {
            try
            {
                APIUtil.Authenticate(this.Request);

                if (this.metaData == null || lastDownload + TimeSpan.FromMinutes(1) < DateTime.Now)
                {
                    var url = APIUtil.GetSettingsUrl();
                    this.metaData = ApplicationMetadataUtil.DownloadMetadata<ApplicationClientMetadata>(url);
                    lastDownload = DateTime.Now;
                }

                var context = APIUtil.ReadBody(this.Request);
                using (var vw = new VowpalWabbit(new VowpalWabbitSettings(metaData.TrainArguments)
                {
                    EnableStringExampleGeneration = true,
                    EnableStringFloatCompact = true
                }))
                using (var serializer = new VowpalWabbitJsonSerializer(vw))
                using (var example = serializer.ParseAndCreate(context))
                {
                    return Json(new { VWExample = example.VowpalWabbitString });
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, ex.Message);
            }
            catch (Exception ex)
            {
                new TelemetryClient().TrackException(ex);
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        [HttpPost]
        public ActionResult Policy(int defaultAction = -1)
        {
            try
            {
                APIUtil.Authenticate(this.Request);

                var client = DecisionServiceClientFactory.AddOrGetExisting();
                var context = APIUtil.ReadBody(this.Request);
                var eventId = APIUtil.CreateEventId();
                var action = defaultAction != -1 ? client.ChooseAction(eventId, context, defaultAction) : client.ChooseAction(eventId, context);

                return Json(new 
                {
                    EventId = eventId,
                    Action = action
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, ex.Message);
            }
            catch (Exception ex)
            {
                new TelemetryClient().TrackException(ex);
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        [HttpPost]
        public ActionResult Ranker(int[] defaultActions)
        {
            try
            {
                APIUtil.Authenticate(this.Request);

                var client = DecisionServiceClientFactory.AddOrGetExisting();
                var context = APIUtil.ReadBody(this.Request);
                var eventId = APIUtil.CreateEventId();
                var actions = defaultActions != null && defaultActions.Length > 0 ?
                        client.ChooseRanking(eventId, context, defaultActions) :
                        client.ChooseRanking(eventId, context);

                return Json(new
                {
                    EventId = eventId,
                    Actions = actions
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, ex.Message);
            }
            catch (Exception ex)
            {
                new TelemetryClient().TrackException(ex);
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        [HttpPost]
        public ActionResult Reward(string eventId)
        {
            try
            {
                APIUtil.Authenticate(this.Request);

                var client = DecisionServiceClientFactory.AddOrGetExisting();
                var rewardStr = APIUtil.ReadBody(this.Request);

                var rewardObj = JToken.Parse(rewardStr);

                client.ReportOutcome(rewardObj, eventId);

                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, ex.Message);
            }
            catch (Exception ex)
            {
                new TelemetryClient().TrackException(ex);
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        [HttpPost]
        public ActionResult Reset()
        {
            try
            {
                var token = APIUtil.Authenticate(this.Request, ConfigurationManager.AppSettings[ApplicationMetadataStore.AKAdminToken]);

                using (var wc = new WebClient())
                {
                    string uniqueStringInUrl = Regex.Match(Request.Url.ToString(), ".*mc-(.*).azurewebsites.*").Groups[1].Value;

                    // TODO: cache me?!
                    var extraSettingsBlob = (CloudBlockBlob)Session[HomeController.SKExtraSettingsBlob];
                    ApplicationExtraMetadata extraApp = JsonConvert.DeserializeObject<ApplicationExtraMetadata>(extraSettingsBlob.DownloadText());

                    wc.Headers.Add($"Authorization: {token}");
                    wc.DownloadString($"http://{extraApp.AzureResourceGroupName}-trainer-{uniqueStringInUrl}.cloudapp.net/reset");
                }

                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, ex.Message);
            }
            catch (Exception ex)
            {
                new TelemetryClient().TrackException(ex);
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.ToString());
            }

        }
    }
}