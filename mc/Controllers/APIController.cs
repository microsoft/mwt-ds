using DecisionServicePrivateWeb.Classes;
using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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

        public static DateTime ModelUpdateTime = new DateTime(1, 1, 1);

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

                var client = DecisionServiceClientFactory.AddOrGetExisting(ModelSuccessNotifier);
                var context = APIUtil.ReadBody(this.Request);
                var eventId = APIUtil.CreateEventId();
                var action = defaultAction != -1 ? client.ChooseAction(eventId, context, defaultAction) : client.ChooseAction(eventId, context);

                return Json(new 
                {
                    EventId = eventId,
                    Action = action,
                    ModelTime = ModelUpdateTime
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

                var client = DecisionServiceClientFactory.AddOrGetExisting(ModelSuccessNotifier);
                var context = APIUtil.ReadBody(this.Request);
                var eventId = APIUtil.CreateEventId();
                var actions = defaultActions != null && defaultActions.Length > 0 ?
                        client.ChooseRanking(eventId, context, defaultActions) :
                        client.ChooseRanking(eventId, context);

                return Json(new
                {
                    EventId = eventId,
                    Actions = actions,
                    ModelTime = ModelUpdateTime
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
            var telemetry = new TelemetryClient();
            try
            {
                APIUtil.Authenticate(this.Request);

                var client = DecisionServiceClientFactory.AddOrGetExisting(ModelSuccessNotifier);
                var rewardStr = APIUtil.ReadBody(this.Request);

                var rewardObj = JToken.Parse(rewardStr);

                client.ReportOutcome(rewardObj, eventId);

                telemetry.TrackTrace($"HTTP Endpoint received reward report of: {rewardStr}");

                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, ex.Message);
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        [HttpPost]
        public async Task<ActionResult> Reset()
        {
            var telemetry = new TelemetryClient();

            try
            {
                var token = APIUtil.Authenticate(this.Request, ConfigurationManager.AppSettings[ApplicationMetadataStore.AKAdminToken]);

                using (var wc = new WebClient())
                {
                    wc.Headers.Add($"Authorization: {token}");
                    wc.DownloadString(ConfigurationManager.AppSettings[ApplicationMetadataStore.AKTrainerURL] + "/reset");

                    var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString]);
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    var blobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.OfflineEvalContainerName);
                    await Task.WhenAll(blobContainer
                        .ListBlobs(useFlatBlobListing: true)
                        .OfType<CloudBlockBlob>()
                        .Select(b => b.UploadFromByteArrayAsync(new byte[0] { }, 0, 0)));
                }

                return new HttpStatusCodeResult(HttpStatusCode.OK);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, ex.Message);
            }
            catch (WebException ex)
            {
                telemetry.TrackException(ex);
                string response;
                using (var stream = new StreamReader(ex.Response.GetResponseStream()))
                {
                    response = await stream.ReadToEndAsync();
                }

                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.ToString() + " " + response);
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        private void ModelSuccessNotifier(byte[] modelBytes)
        {
            ModelUpdateTime = DateTime.UtcNow;
        }
    }
}