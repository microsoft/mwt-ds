using DecisionServicePrivateWeb.Classes;
using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.WindowsAzure.Storage;
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
                    var url = GetSettingsUrl();
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

        private string GetSettingsUrl()
        {
            var settingsURL = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKDecisionServiceSettingsUrl];
            if (settingsURL == null)
            {
                var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString]);
                var blobClient = storageAccount.CreateCloudBlobClient();
                var settingsBlobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.SettingsContainerName);
                var extraSettingsBlob = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestExtraSettingsBlobName);
                var extraSettings = JsonConvert.DeserializeObject<ApplicationExtraMetadata>(extraSettingsBlob.DownloadText());
                settingsURL = extraSettings.SettingsTokenUri1;
                ConfigurationManager.AppSettings.Set(ApplicationMetadataStore.AKDecisionServiceSettingsUrl, settingsURL);
            }
            return settingsURL;
        }
    }
}