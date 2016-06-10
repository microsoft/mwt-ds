using DecisionServicePrivateWeb.Classes;
using DecisionServicePrivateWeb.Models;
using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace DecisionServicePrivateWeb.Controllers
{
    [RequireHttps]
    public class HomeController : Controller
    {
        const string SKAuthenticated = "Authenticated";

        const string SKClientSettingsBlob = "ClientSettingsBlob";
        const string SKExtraSettingsBlob = "ExtraSettingsBlob";
        const string SKEvalContainer = "EvalContainer";

        const string SKClientSettings = "ClientSettings";
        const string SKExtraSettings = "ExtraSettings";

        [HttpGet]
        public ActionResult Index()
        {
            return View(new IndexViewModel { Authenticated = IsAuthenticated(Session) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(string password)
        {
            string correctPassword = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKPassword];
            if (string.Equals(password, correctPassword))
            {
                Session[SKAuthenticated] = true;

                // Create again in case the settings were not created at start up
                ApplicationMetadataStore.CreateSettingsBlobIfNotExists();

                string azureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString];
                var storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
                var blobClient = storageAccount.CreateCloudBlobClient();
                var settingsBlobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.SettingsContainerName);

                Session[SKClientSettingsBlob] = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestClientSettingsBlobName);
                Session[SKExtraSettingsBlob] = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestExtraSettingsBlobName);
                Session[SKEvalContainer] = blobClient.GetContainerReference(ApplicationBlobConstants.OfflineEvalContainerName);

                return Redirect(Url.Action("Settings"));
            }
            return View(new IndexViewModel { Authenticated = false, Error = "Invalid Password" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            Session.Clear();
            return RedirectToAction("Index");
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Settings()
        {
            if (!IsAuthenticated(Session))
            {
                return RedirectToAction("Index");
            }
            string userName = User.Identity.Name;
            ApplicationClientMetadata clientApp = null;
            ApplicationExtraMetadata extraApp = null;
            string settingsBlobUri = string.Empty;
            try
            {
                var clientSettingsBlob = (CloudBlockBlob)Session[SKClientSettingsBlob];
                settingsBlobUri = clientSettingsBlob.Uri.ToString();
                clientApp = JsonConvert.DeserializeObject<ApplicationClientMetadata>(clientSettingsBlob.DownloadText());
                Session[SKClientSettings] = clientApp;

                var extraSettingsBlob = (CloudBlockBlob)Session[SKExtraSettingsBlob];
                extraApp = JsonConvert.DeserializeObject<ApplicationExtraMetadata>(extraSettingsBlob.DownloadText());
                Session[SKExtraSettings] = extraApp;
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to load application metadata: {ex.ToString()}");
            }

            return View(CreateAppView(clientApp, extraApp, settingsBlobUri));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Settings(SettingsSaveModel model)
        {
            try
            {
                var clientMeta = (ApplicationClientMetadata)Session[SKClientSettings];
                clientMeta.IsExplorationEnabled = model.IsExplorationEnabled;
                clientMeta.TrainArguments = model.TrainArguments;
                var clientSettingsBlob = (CloudBlockBlob)Session[SKClientSettingsBlob];
                clientSettingsBlob.UploadText(JsonConvert.SerializeObject(clientMeta));

                var extraMeta = (ApplicationExtraMetadata)Session[SKExtraSettings];
                extraMeta.ModelId = model.SelectedModelId;
                var extraSettingsBlob = (CloudBlockBlob)Session[SKExtraSettingsBlob];
                extraSettingsBlob.UploadText(JsonConvert.SerializeObject(extraMeta));

                try
                {
                    // copy selected model file to the latest file
                    ApplicationMetadataStore.UpdateModel(model.SelectedModelId, ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString]);
                }
                catch (Exception ex)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to update model: {ex.ToString()}");
                }

                return View(CreateAppView(clientMeta, extraMeta, clientSettingsBlob.Uri.ToString()));
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to update application metadata: {ex.ToString()}");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Evaluation()
        {
            if (!IsAuthenticated(Session))
            {
                return RedirectToAction("Index");
            }

            return View(new EvaluationViewModel { WindowFilters = new List<string>(new string[] { "5m", "20m", "1h", "3h", "6h" }), SelectedFilter = "5m" });
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult EvalJson(string windowType = "3h", int maxNumPolicies = 5)
        {
            var policyRegex = "Constant Policy (.*)";
            var regex = new Regex(policyRegex);

            if (!IsAuthenticated(Session))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
            }

            try
            {
                var evalContainer = (CloudBlobContainer)Session[SKEvalContainer];
                var evalBlobs = evalContainer.ListBlobs(useFlatBlobListing: true);
                var evalData = new Dictionary<string, EvalD3>();
                foreach (var evalBlob in evalBlobs)
                {
                    // TODO: cache or optimize perf
                    var evalBlockBlob = (CloudBlockBlob)evalBlob;
                    if (evalBlockBlob != null)
                    {
                        var evalTextData = evalBlockBlob.DownloadText();
                        var evalLines = evalTextData.Split(new string[] { "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var l in evalLines)
                        {
                            var evalResult = JsonConvert.DeserializeObject<EvalResult>(l);
                            if (evalResult.WindowType != windowType)
                            {
                                continue;
                            }
                            string policyNumber = regex.Match(evalResult.PolicyName).Groups[1].Value;
                            int policyNumberInt;
                            if (int.TryParse(policyNumber, out policyNumberInt) && policyNumberInt > maxNumPolicies)
                            {
                                continue;
                            }
                            if (evalData.ContainsKey(evalResult.PolicyName))
                            {
                                var timeToCost = evalData[evalResult.PolicyName].values;
                                    //.Add(new object[] { evalResult.LastWindowTime, evalResult.AverageCost });

                                if (timeToCost.ContainsKey(evalResult.LastWindowTime))
                                {
                                    timeToCost[evalResult.LastWindowTime] = evalResult.AverageCost;
                                }
                                else
                                {
                                    timeToCost.Add(evalResult.LastWindowTime, evalResult.AverageCost);
                                }
                            }
                            else
                            {
                                evalData.Add(evalResult.PolicyName, new EvalD3 { key = evalResult.PolicyName, values = new Dictionary<DateTime, float>() });
                            }
                        }
                    }
                }

                return Json(evalData.Values.Select(a => new { key = a.key, values = a.values.Select(v => new object[] { v.Key, v.Value }) }), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to load evaluation result: {ex.ToString()}");
            }
        }

        public static string GetDecisionTypeString(DecisionType decisionType)
        {
            switch (decisionType)
            {
                case DecisionType.SingleAction:
                    return "Features";
                case DecisionType.MultiActions:
                    return "Action Dependent Features";
                default:
                    return string.Empty;
            }
        }

        public static bool IsAuthenticated(HttpSessionStateBase Session)
        {
            return (Session[SKAuthenticated] != null && (bool)Session[SKAuthenticated]);
        }


        private static List<SettingItemViewModel> CreateAppView(
            ApplicationClientMetadata clientMetadata,
            ApplicationExtraMetadata extraMetadata,
            string settingsBlobUri)
        {
            CollectiveSettingsView svm = CreateCollectiveSettings(clientMetadata, extraMetadata, settingsBlobUri);
            string azureStorageName = Regex.Match(svm.AzureStorageConnectionString, ".*AccountName=(.*);AccountKey.*").Groups[1].Value;

            var nameToValue = svm.GetType().GetProperties()
                .Select(p => new { Name = p.Name, Value = p.GetValue(svm) });

            var nameToHelpText = typeof(HelpText).GetProperties()
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => new { HelpText = ((string)p.GetValue(null)).Split(new string[] { " ::: " }, StringSplitOptions.RemoveEmptyEntries).ToList(), Name = p.Name });

            var nameToUrl = new[]
            {
                new { Name = nameof(svm.AzureResourceGroupName), Tooltip = "View Azure Resource Group", Url = $"https://ms.portal.azure.com/#asset/HubsExtension/ResourceGroups/subscriptions/{svm.AzureSubscriptionId}/resourceGroups/{svm.AzureResourceGroupName}"},
                new { Name = nameof(svm.AzureStorageConnectionString), Tooltip = "View Data", Url = $"https://ms.portal.azure.com/#blade/Microsoft_Azure_Storage/ContainersBlade/storageAccountId/%2Fsubscriptions%2F{svm.AzureSubscriptionId}%2FresourceGroups%2F{svm.AzureResourceGroupName}%2Fproviders%2FMicrosoft.Storage%2FstorageAccounts%2F{azureStorageName}"},
                new { Name = nameof(svm.ApplicationInsightsInstrumentationKey), Tooltip = "View Application Logs", Url = $"https://ms.portal.azure.com/#blade/AppInsightsExtension/SearchBlade/ComponentId/%7B%22SubscriptionId%22%3A%22{svm.AzureSubscriptionId}%22%2C%22ResourceGroup%22%3A%22{svm.AzureResourceGroupName}%22%2C%22Name%22%3A%22{svm.AzureResourceGroupName + "-appinsights"}%22%7D/InitialFilter/%7B%22eventTypes%22%3A%5B4%2C1%2C3%2C5%2C2%2C6%5D%2C%22typeFacets%22%3A%7B%7D%2C%22isPermissive%22%3Afalse%7D/InitialTime/%7B%22durationMs%22%3A43200000%2C%22endTime%22%3Anull%2C%22isInitialTime%22%3Atrue%2C%22grain%22%3A1%7D/InitialQueryText//ConfigurationId/blankSearch%3A"},
                new { Name = nameof(svm.ASAJoinName), Tooltip = "View ASA Join Query", Url = $"https://ms.portal.azure.com/#resource/subscriptions/{svm.AzureSubscriptionId}/resourceGroups/{svm.AzureResourceGroupName}/providers/Microsoft.StreamAnalytics/streamingjobs/{svm.ASAJoinName}"},
                new { Name = nameof(svm.ASAEvalName), Tooltip = "View ASA Policy Evaluation Query", Url = $"https://ms.portal.azure.com/#resource/subscriptions/{svm.AzureSubscriptionId}/resourceGroups/{svm.AzureResourceGroupName}/providers/Microsoft.StreamAnalytics/streamingjobs/{svm.ASAEvalName}"},
                new { Name = nameof(svm.OnlineTrainerName), Tooltip = "Configure Online Trainer", Url = $"https://manage.windowsazure.com/microsoft.onmicrosoft.com#Workspaces/CloudServicesExtension/CloudService/{svm.OnlineTrainerName}/configure"}
            };
            var nameToEditable = new[]
            {
                new { Name = nameof(svm.TrainArguments), IsEditable = true },
                new { Name = nameof(svm.IsExplorationEnabled), IsEditable = true },
                new { Name = nameof(svm.SelectedModelId), IsEditable = true }
            };
            var nameToVisible = new[]
            {
                new { Name = nameof(svm.NumActions), IsVisible = (svm.DecisionType == DecisionType.SingleAction) }
            };
            var nameToSpotLightUrl = new[]
            {
                new { Name = nameof(svm.AzureResourceGroupName), IsSplotlightUrl = true }
            };

            var settingItems = from nv in nameToValue
                        from nht in nameToHelpText.Where(n => n.Name == nv.Name).DefaultIfEmpty()
                        from nu in nameToUrl.Where(n => n.Name == nv.Name).DefaultIfEmpty()
                        from ne in nameToEditable.Where(n => n.Name == nv.Name).DefaultIfEmpty()
                        from nvs in nameToVisible.Where(n => n.Name == nv.Name).DefaultIfEmpty()
                        from nsl in nameToSpotLightUrl.Where(n => n.Name == nv.Name).DefaultIfEmpty()
                        select new SettingItemViewModel
                        {
                            Value = nv.Value,
                            DisplayName = nht?.HelpText[0],
                            HelpText = nht?.HelpText[1],
                            IsEditable = ne?.IsEditable,
                            Name = nv.Name,
                            Url = nu?.Url,
                            UrlToolTip = nu?.Tooltip,
                            IsVisible = nvs?.IsVisible,
                            IsSplotlightUrl = nsl?.IsSplotlightUrl
                        };

            return settingItems.ToList();
        }

        private static CollectiveSettingsView CreateCollectiveSettings(ApplicationClientMetadata clientMetadata, ApplicationExtraMetadata extraMetadata, string settingsBlobUri)
        {
            var svm = new CollectiveSettingsView
            {
                ApplicationId = clientMetadata.ApplicationID,
                AzureSubscriptionId = extraMetadata.SubscriptionId,
                DecisionType = extraMetadata.DecisionType,
                NumActions = clientMetadata.NumActions,
                TrainFrequency = extraMetadata.TrainFrequency,
                TrainArguments = clientMetadata.TrainArguments,
                AzureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString],
                AzureResourceGroupName = extraMetadata.AzureResourceGroupName,
                ApplicationInsightsInstrumentationKey = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKAppInsightsKey],
                OnlineTrainerName = extraMetadata.AzureResourceGroupName + "-trainer",
                WebApiAddress = $"https://{extraMetadata.AzureResourceGroupName}-webapi.azurewebsites.net",
                ASAEvalName = extraMetadata.AzureResourceGroupName + "-eval",
                ASAJoinName = extraMetadata.AzureResourceGroupName + "-join",
                EventHubInteractionConnectionString = clientMetadata.EventHubInteractionConnectionString,
                EventHubObservationConnectionString = clientMetadata.EventHubObservationConnectionString,
                ExperimentalUnitDuration = extraMetadata.ExperimentalUnitDuration,
                SelectedModelId = new SettingBlobListViewModel
                {
                    Items = new List<BlobModelViewModel>()
                    {
                        new BlobModelViewModel
                        {
                            Name = ApplicationSettingConstants.UseLatestModelSetting
                        }
                    },
                    SelectedItem = extraMetadata.ModelId
                },
                IsExplorationEnabled = clientMetadata.IsExplorationEnabled,
                SettingsBlobUri = settingsBlobUri
            };
            return svm;
        }
    }
}