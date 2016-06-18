using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.ApplicationInsights;
using System.Diagnostics;
using System.Configuration;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Web.Mvc;

namespace DecisionServicePrivateWeb.Controllers
{
    //public class RewardController : ApiBaseController
    //{
    //    // TODO: for headless implementation support time-out encrypted tokens and redirect
    //    // requires of such a token
    //    // public HttpResponseMessage Get([FromUri] float reward, [FromUri] String eventId, [FromUri] String target)

    //    // POST api/<controller>
    //    // [Route("reward/{eventId}")]
    //    [HttpPost]
    //    public void Post(string eventId)
    //    {
    //        this.Authenticate();
    //        var telemetry = new TelemetryClient();
    //        var stopwatch = Stopwatch.StartNew();

    //        try
    //        {
    //            telemetry.Context.Operation.Name = "Reward";
    //            telemetry.Context.Operation.Id = eventId;

    //            // support simply float and complex JSON outcomes
    //            var rewardStr = this.ReadBody();
    //            var rewardObj = JToken.Parse(rewardStr);

    //            // parse input
    //            var guid = Guid.ParseExact(eventId, "N");

    //            var url = ConfigurationManager.AppSettings["DecisionServiceSettingsUrl"];
    //            var eventUploader = DecisionServiceStaticClient.AddOrGetExisting("uploader" + url,
    //                _ =>
    //                {
    //                    telemetry.TrackEvent("EventUploader creation");
                                               // TODO CHANGE to use ]
    //                    var metaData = ApplicationMetadataUtil.DownloadMetadata<ApplicationClientMetadata>(url);
    //                    return new EventUploaderASA(
    //                        metaData.EventHubObservationConnectionString,
    //                        new BatchingConfiguration
    //                        {
    //                        // TODO: these are not production ready configurations. do we need to move those to C&C as well?
    //                        MaxBufferSizeInBytes = 1,
    //                            MaxDuration = TimeSpan.FromSeconds(1),
    //                            MaxEventCount = 1,
    //                            MaxUploadQueueCapacity = 1,
    //                            UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
    //                        });
    //                });

    //            eventUploader.Upload(new Observation
    //            {
    //                Key = guid.ToString("N", CultureInfo.InvariantCulture),
    //                Value = rewardObj
    //            });

    //            stopwatch.Stop();
    //            telemetry.TrackRequest("ReportReward", DateTime.Now, stopwatch.Elapsed, "200", true);
    //        }
    //        catch(Exception e)
    //        {
    //            telemetry.TrackException(e);
    //        }
    //    }
    //}

}