using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DecisionServicePrivateWeb.Classes
{
    static class DecisionServiceClientFactory
    {
        private static DecisionServiceConfiguration CreateConfiguration(string settingsUrl)
        {
            var telemetry = new TelemetryClient();
            telemetry.TrackEvent($"DecisionServiceClient created: '{settingsUrl}'");

            return new DecisionServiceConfiguration(settingsUrl)
            {
                InteractionUploadConfiguration = new BatchingConfiguration
                {
                    // TODO: these are not production ready configurations. do we need to move those to C&C as well?
                    MaxBufferSizeInBytes = 1,
                    MaxDuration = TimeSpan.FromSeconds(1),
                    MaxEventCount = 1,
                    MaxUploadQueueCapacity = 1,
                    UploadRetryPolicy = BatchUploadRetryPolicy.ExponentialRetry
                },
                ModelPollFailureCallback = e => telemetry.TrackException(e, new Dictionary<string, string> { { "Pool failure", "model" } }),
                SettingsPollFailureCallback = e => telemetry.TrackException(e, new Dictionary<string, string> { { "Pool failure", "settings" } })
            };
        }

        public static DecisionServiceClient<string> AddOrGetExisting(string settingsUrl)
        {
            return DecisionServiceStaticClient.AddOrGetExisting(settingsUrl, _ => DecisionService.CreateJson(CreateConfiguration(settingsUrl)));
        }
    }
}