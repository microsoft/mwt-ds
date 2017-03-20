using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceWebStandalone
{
    public class DecisionServiceController : Controller
    {
        private static DecisionServiceClient<string> client;

        public static void Initialize(string settingsUrl)
        {
            var telemetry = new TelemetryClient();

            var config = new DecisionServiceConfiguration(settingsUrl)
            {
                InteractionUploadConfiguration = new BatchingConfiguration
                {
                    MaxDuration = TimeSpan.FromSeconds(2),
                    UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
                },
                ModelPollFailureCallback = e => telemetry.TrackException(e, new Dictionary<string, string> { { "Pool failure", "model" } }),
                SettingsPollFailureCallback = e => telemetry.TrackException(e, new Dictionary<string, string> { { "Pool failure", "settings" } }),
                // AzureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString]
            };

            client = DecisionService.CreateJson(config);
        }

        private async Task<string> ReadBody()
        {
            using (var reader = new StreamReader(this.Request.Body))
            {
                return await reader.ReadToEndAsync();
            }
        }


        [HttpPost("rank")]
        public async Task<object> Rank(string defaultActions, string eventId)
        {
            var context = await ReadBody();

            if (string.IsNullOrEmpty(eventId))
                eventId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

            int[] actions;
            if (string.IsNullOrWhiteSpace(defaultActions))
            {
                actions = client.ChooseRanking(eventId, context);
            }
            else
            {
                int[] defaultActionArray = Array.ConvertAll(defaultActions.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries), Convert.ToInt32);
                actions = client.ChooseRanking(eventId, context, defaultActionArray);
            }

            return Json(new
            {
                EventId = eventId,
                Actions = actions
            });
        }

        [HttpPost("reward")]
        public async void Reward(string eventId)
        {
            var rewardStr = await ReadBody();

            var rewardObj = JToken.Parse(rewardStr);

            client.ReportOutcome(rewardObj, eventId);
        }
    }
}
