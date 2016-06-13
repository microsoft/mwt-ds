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
    public class SASTokenController : Controller
    {
        [HttpGet]
        public string Generate(string key)
        {
            var telemetry = new TelemetryClient();
            try
            {
                string azureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString];
                var storageAccountKey = Regex.Match(azureStorageConnectionString, ".*AccountKey=(.*)").Groups[1].Value;
                if (key.Replace(" ", "+") == storageAccountKey)
                {
                    string clSASTokenUri;
                    string webSASTokenUri;
                    ApplicationMetadataStore.CreateSettingsBlobIfNotExists(out clSASTokenUri, out webSASTokenUri);
                    if (clSASTokenUri != null && webSASTokenUri != null)
                    {
                        telemetry.TrackTrace("Generated SAS tokens for settings URI");

                        string template = System.IO.File.ReadAllText(Path.Combine(Server.MapPath("~/App_Data"), "SASTokenGenerator.json"));
                        return template.Replace("$$ClientSettings$$", clSASTokenUri).Replace("$$WebSettings$$", webSASTokenUri);
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
    }
}