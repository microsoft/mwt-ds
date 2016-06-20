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
        public async Task<ActionResult> Validate()
        {
            var userToken = this.Request.Headers[AuthHeaderName];
            if (userToken != ConfigurationManager.AppSettings[ApplicationMetadataStore.AKPassword])
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "A valid token must be specified.");
            }

            try
            {
                if (this.metaData == null || lastDownload + TimeSpan.FromMinutes(1) < DateTime.Now)
                {
                    var url = GetSettingsUrl();
                    this.metaData = ApplicationMetadataUtil.DownloadMetadata<ApplicationClientMetadata>(url);
                    lastDownload = DateTime.Now;
                }

                Stream inputStream = Request.InputStream;
                inputStream.Seek(0, System.IO.SeekOrigin.Begin);
                using (var inputReader = new StreamReader(inputStream))
                using (var vw = new VowpalWabbit(new VowpalWabbitSettings(metaData.TrainArguments)
                {
                    EnableStringExampleGeneration = true,
                    EnableStringFloatCompact = true
                }))
                using (var serializer = new VowpalWabbitJsonSerializer(vw))
                using (var example = serializer.ParseAndCreate(new JsonTextReader(new StringReader(await inputReader.ReadToEndAsync()))))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.OK, example.VowpalWabbitString);
                }
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        [HttpPost]
        public async Task<ActionResult> Policy(int defaultAction = -1)
        {
            return await ChooseAction(
                "Policy",
                (telemetry, input) =>
                {
                    var url = GetSettingsUrl();
                    var client = DecisionServiceClientFactory.AddOrGetExisting(url);
                    return defaultAction != -1 ?
                        client.ChooseAction(input.EventId, input.Context, defaultAction) :
                        client.ChooseAction(input.EventId, input.Context);
                });
        }

        [HttpPost]
        public async Task<ActionResult> Ranker(int[] defaultActions)
        {
            return await ChooseAction(
                "Ranker",
                (telemetry, input) =>
                {
                    var url = GetSettingsUrl();
                    var client = DecisionServiceClientFactory.AddOrGetExisting(url);
                    var action = defaultActions != null && defaultActions.Length > 0 ?
                        client.ChooseRanking(input.EventId, input.Context, defaultActions) :
                        client.ChooseRanking(input.EventId, input.Context);

                    return action;
                });
        }

        [HttpPost]
        public async Task<ActionResult> Reward(string eventId)
        {
            var userToken = Request.Headers[AuthHeaderName];
            if (userToken != ConfigurationManager.AppSettings[ApplicationMetadataStore.AKPassword])
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "A valid token must be specified.");
            }

            var telemetry = new TelemetryClient();
            try
            {
                var stopwatch = Stopwatch.StartNew();
                telemetry.Context.Operation.Name = "Reward";
                telemetry.Context.Operation.Id = eventId;

                Stream inputStream = Request.InputStream;
                inputStream.Seek(0, System.IO.SeekOrigin.Begin);
                using (var inputReader = new StreamReader(inputStream))
                {
                    // support simply float and complex JSON outcomes
                    var rewardStr = await inputReader.ReadToEndAsync();
                    var rewardObj = JToken.Parse(rewardStr);

                    // parse input
                    var guid = Guid.ParseExact(eventId, "N");

                    var url = GetSettingsUrl();
                    var eventUploader = DecisionServiceStaticClient.AddOrGetExisting("uploader" + url,
                        _ =>
                        {
                            telemetry.TrackEvent("EventUploader creation");

                            var metaData = ApplicationMetadataUtil.DownloadMetadata<ApplicationClientMetadata>(url);
                            return new EventUploaderASA(
                                metaData.EventHubObservationConnectionString,
                                new BatchingConfiguration
                                {
                                // TODO: these are not production ready configurations. do we need to move those to C&C as well?
                                MaxBufferSizeInBytes = 1,
                                    MaxDuration = TimeSpan.FromSeconds(1),
                                    MaxEventCount = 1,
                                    MaxUploadQueueCapacity = 1,
                                    UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
                                });
                        });

                    eventUploader.Upload(new Observation
                    {
                        Key = guid.ToString("N", CultureInfo.InvariantCulture),
                        Value = rewardObj
                    });

                    stopwatch.Stop();
                    telemetry.TrackRequest("ReportReward", DateTime.Now, stopwatch.Elapsed, "200", true);

                    return new HttpStatusCodeResult(HttpStatusCode.OK);
                }
            }
            catch (Exception e)
            {
                telemetry.TrackException(e);
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, e.Message);
            }
        }

        internal async Task<ActionResult> ChooseAction<T>(string name, Func<TelemetryClient, Input, T> operation)
        {
            var telemetry = new TelemetryClient();
            try
            {
                var stopwatch = Stopwatch.StartNew();

                Stream inputStream = Request.InputStream;
                inputStream.Seek(0, System.IO.SeekOrigin.Begin);
                using (var inputReader = new StreamReader(inputStream))
                {
                    var input = new Input(Request, await inputReader.ReadToEndAsync());

                    // actual choose action call
                    var action = operation(telemetry, input);

                    telemetry.Context.Operation.Name = "ChooseAction";
                    telemetry.Context.Operation.Id = input.EventId.ToString();

                    var response = new
                    {
                        Action = action,
                        EventId = input.EventId
                    };

                    stopwatch.Stop();
                    telemetry.TrackRequest(name, DateTime.Now, stopwatch.Elapsed, "200", true);

                    return Json(response);
                }
            }
            catch (Exception e)
            {
                telemetry.TrackException(e);
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, e.Message);
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
                ConfigurationManager.AppSettings.Add(ApplicationMetadataStore.AKDecisionServiceSettingsUrl, settingsURL);
            }
            return settingsURL;
        }

        internal sealed class Input
        {
            internal Input(HttpRequestBase request, string context)
            {
                this.Context = context;

                this.UserToken = request.Headers[AuthHeaderName];
                if (this.UserToken != ConfigurationManager.AppSettings[ApplicationMetadataStore.AKPassword])
                    throw new UnauthorizedAccessException("Authorization token missing");

                this.EventId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            }

            internal string Context { get; private set; }

            internal string UserToken { get; private set; }

            internal string EventId { get; private set; }
        }
    }
}