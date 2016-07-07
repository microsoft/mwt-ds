using DecisionServicePrivateWeb.Classes;
using Microsoft.ApplicationInsights;
using Microsoft.WindowsAzure.Storage;
using System;
using System.Configuration;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DecisionServicePrivateWeb.Controllers
{
    [RequireHttps]
    public class DeploymentController : Controller
    {
        // works as long as the MC is not in multiple processes and not on multiple machines
        private static object lockObj = new object();

        [HttpGet]
        public string GenerateSASToken(string parameters)
        {
            var telemetry = new TelemetryClient();
            string template = System.IO.File.ReadAllText(Path.Combine(Server.MapPath("~/App_Data"), "SASTokenGenerator.json"));
            try
            {
                lock (lockObj)
                {
                    string azureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString];
                    var storageAccountKey = Regex.Match(azureStorageConnectionString, ".*AccountKey=(.*)").Groups[1].Value;
                    var paramMatches = Regex.Match(parameters.Replace(" ", "+"), "key=(.*);trainer_size=(.*)");
                    if (paramMatches.Groups.Count < 3)
                    {
                        throw new Exception("Request parameters are not valid: " + parameters);
                    }
                    var key = paramMatches.Groups[1].Value;
                    var trainerSize = paramMatches.Groups[2].Value;
                    if (key == storageAccountKey)
                    {
                        // Generate SAS token URI for client settings blob.
                        // Two tokens are generated separately, one for consumption by the Client Library and the other for Web Service
                        string clSASTokenUri;
                        string webSASTokenUri;
                        ApplicationMetadataStore.CreateSettingsBlobIfNotExists(out clSASTokenUri, out webSASTokenUri);

                        telemetry.TrackTrace("Requested Online Trainer Size of " + trainerSize);

                        // Copy the .cspkg to user blob and generate SAS tokens for deployment
                        // NOTE: this has to live in blob storage, cannot be any random URL
                        // TODO: take the link URL as parameter
                        string cspkgLink = $"https://github.com/eisber/vowpal_wabbit/releases/download/v8.2.0.6/VowpalWabbit.Azure.8.2.0.6.{trainerSize}.cspkg";
                        var cspkgUri = ApplicationMetadataStore.CreateOnlineTrainerCspkgBlobIfNotExists(cspkgLink);

                        telemetry.TrackTrace($"SASToken: clSASTokenUri: '{clSASTokenUri}' webSASTokenUri: '{webSASTokenUri}' cspkgUri: '{cspkgUri}'");
                        if (clSASTokenUri != null && webSASTokenUri != null && cspkgUri != null)
                        {
                            telemetry.TrackTrace("Generated SAS tokens for settings URI and online trainer package");
                            return template
                                .Replace("$$ClientSettings$$", clSASTokenUri)
                                .Replace("$$WebSettings$$", webSASTokenUri)
                                .Replace("$$OnlineTrainerCspkg$$", cspkgUri);
                        }
                    }
                    else
                    {
                        telemetry.TrackTrace($"Provided key is not correct: { key }");
                    }
                }
            }
            catch (Exception ex)
            {
                telemetry.TrackException(ex);
            }
            return template;
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