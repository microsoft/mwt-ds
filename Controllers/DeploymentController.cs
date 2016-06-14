using DecisionServicePrivateWeb.Classes;
using Microsoft.ApplicationInsights;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Research.MultiWorldTesting.Contract;

namespace DecisionServicePrivateWeb.Controllers
{
    [RequireHttps]
    public class DeploymentController : Controller
    {
        [HttpGet]
        public string GenerateSASToken(string key, string trainer_size)
        {
            var telemetry = new TelemetryClient();
            try
            {
                string azureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString];
                var storageAccountKey = Regex.Match(azureStorageConnectionString, ".*AccountKey=(.*)").Groups[1].Value;
                if (key.Replace(" ", "+") == storageAccountKey)
                {
                    // Generate SAS token URI for client settings blob.
                    // Two tokens are generated separately, one for consumption by the Client Library and the other for Web Service
                    string clSASTokenUri;
                    string webSASTokenUri;
                    ApplicationMetadataStore.CreateSettingsBlobIfNotExists(out clSASTokenUri, out webSASTokenUri);

                    telemetry.TrackTrace("Requested Online Trainer Size of " + trainer_size);

                    // Copy the .cspkg to user blob and generate SAS tokens for deployment
                    // NOTE: this has to live in blob storage, cannot be any random URL
                    string cspkgSASToken;
                    string cspkgLink = "https://github.com/eisber/vowpal_wabbit/releases/download/v8.0.0.65/VowpalWabbit-AzureCloudService-8.0.0.65.cspkg";
                    ApplicationMetadataStore.CreateOnlineTrainerCspkgBlobIfNotExists(cspkgLink, out cspkgSASToken);

                    if (clSASTokenUri != null && webSASTokenUri != null && cspkgSASToken != null)
                    {
                        telemetry.TrackTrace("Generated SAS tokens for settings URI and online trainer package");

                        string template = System.IO.File.ReadAllText(Path.Combine(Server.MapPath("~/App_Data"), "SASTokenGenerator.json"));
                        return template
                            .Replace("$$ClientSettings$$", clSASTokenUri)
                            .Replace("$$WebSettings$$", webSASTokenUri)
                            .Replace("$$OnlineTrainerCspkg$$", cspkgSASToken);
                    }


                }
                else
                {
                    telemetry.TrackTrace($"Provided key is not correct: { key }");
                }
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
            }
            return string.Empty;
        }

        [HttpGet]
        public string GenerateTrainerConfig(string key)
        {
            var telemetry = new TelemetryClient();
            try
            {
                string azureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString];
                var storageAccountKey = Regex.Match(azureStorageConnectionString, ".*AccountKey=(.*)").Groups[1].Value;
                if (key.Replace(" ", "+") == storageAccountKey)
                {
                    string template = System.IO.File.ReadAllText(Path.Combine(Server.MapPath("~/App_Data"), "OnlineTrainerConfiguration.cscfg"));
                    return template
                        .Replace("{instrumentationKey}", ConfigurationManager.AppSettings[ApplicationMetadataStore.AKAppInsightsKey])
                        .Replace("{userStorageConnectionString}", ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString])
                        .Replace("{joinedEventHubConnectionString}", ConfigurationManager.AppSettings[ApplicationMetadataStore.AKJoinedEHSendConnString])
                        .Replace("{evalEventHubConnectionString}", ConfigurationManager.AppSettings[ApplicationMetadataStore.AKEvalEHSendConnString])
                        .Replace("{adminToken}", ConfigurationManager.AppSettings[ApplicationMetadataStore.AKAdminToken])
                        .Replace("{checkpointIntervalOrCount}", ConfigurationManager.AppSettings[ApplicationMetadataStore.AKCheckpointPolicy]);
                }
                else
                {
                    telemetry.TrackTrace($"Provided key is not correct: { key }");
                }
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
            }
            return string.Empty;
        }
    }
}