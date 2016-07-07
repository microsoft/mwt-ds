using DecisionServicePrivateWeb.Attributes;
using DecisionServicePrivateWeb.Classes;
using DecisionServicePrivateWeb.Models;
using Microsoft.ApplicationInsights;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Mvc;

namespace DecisionServicePrivateWeb.Controllers
{
    [RequireHttps]
    public class HomeController : Controller
    {
        const string SKAuthenticated = "Authenticated";

        const string SKClientSettingsBlob = "ClientSettingsBlob";
        internal const string SKExtraSettingsBlob = "ExtraSettingsBlob";
        const string SKEvalContainer = "EvalContainer";

        const string SKClientSettings = "ClientSettings";
        const string SKExtraSettings = "ExtraSettings";

        const string DefaultEvalWindow = "6d";

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

                string azureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString];
                CloudStorageAccount storageAccount;
                if (CloudStorageAccount.TryParse(azureStorageConnectionString, out storageAccount) && storageAccount != null)
                {
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    var settingsBlobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.SettingsContainerName);

                    Session[SKClientSettingsBlob] = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestClientSettingsBlobName);
                    Session[SKExtraSettingsBlob] = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestExtraSettingsBlobName);
                    Session[SKEvalContainer] = blobClient.GetContainerReference(ApplicationBlobConstants.OfflineEvalContainerName);

                    return Redirect(Url.Action("Settings"));
                }
                else
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, "Unable to parse the storage account connection string.");
                }
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
            try
            {
                return View(SettingsView());
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to load application metadata: {ex.ToString()}");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult APITestDrive()
        {
            if (!IsAuthenticated(Session))
            {
                return RedirectToAction("Index");
            }
            try
            {
                return View("APITestDrive", "_TestDriveLayout", SimulationView());
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to load application metadata: {ex.ToString()}");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult APIGuide()
        {
            if (!IsAuthenticated(Session))
            {
                return RedirectToAction("Index");
            }
            return View("APIGuide", "_TestDriveLayout", SimulationView());
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult APITest()
        {
            if (!IsAuthenticated(Session))
            {
                return RedirectToAction("Index");
            }
            return View("APITest", "_TestDriveLayout", SimulationView());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Settings(SettingsSaveModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var clientMeta = (ApplicationClientMetadata)Session[SKClientSettings];
                    clientMeta.IsExplorationEnabled = model.IsExplorationEnabled;
                    clientMeta.InitialExplorationEpsilon = model.InitialExplorationEpsilon;
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

                    return View(CreateAppView(clientMeta, extraMeta));
                }
                catch (Exception ex)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to update application metadata: {ex.ToString()}");
                }
            }
            try
            {
                return View(SettingsView());
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to load application metadata: {ex.ToString()}");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Reset(string inputId, string inputValue)
        {
            if (inputId == nameof(CollectiveSettingsView.OnlineTrainerAddress))
            {
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("Authorization: " + ConfigurationManager.AppSettings[ApplicationMetadataStore.AKAdminToken]);
                    wc.DownloadString(inputValue);
                    Thread.Sleep(TimeSpan.FromSeconds(3));
                }
            }
            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Evaluation()
        {
            if (!IsAuthenticated(Session))
            {
                return RedirectToAction("Index");
            }

            return View(new EvaluationViewModel { WindowFilters = new List<string>(GetEvalFilterWindowTypes()), SelectedFilter = DefaultEvalWindow });
        }

        [HttpGet]
        [AllowAnonymous]
        [NoCache]
        public ActionResult EvalJson(string windowType = DefaultEvalWindow, int maxNumPolicies = 5)
        {
            if (!IsAuthenticated(Session))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized);
            }

            return GetEvalData(windowType, maxNumPolicies);
        }

        [HttpGet]
        [AllowAnonymous]
        [NoCache]
        public ActionResult EvalJsonAPI(string userToken, string windowType = DefaultEvalWindow, int maxNumPolicies = 5)
        {
            if (userToken != ConfigurationManager.AppSettings[ApplicationMetadataStore.AKWebServiceToken])
            {
                return new HttpStatusCodeResult(HttpStatusCode.Unauthorized, "A valid token must be specified.");
            }

            string trainerStatus = string.Empty;
            try
            {
                using (var wc = new TimeoutWebClient())
                {
                    var trainerStatusJson = wc.DownloadString(ConfigurationManager.AppSettings[ApplicationMetadataStore.AKTrainerURL] + "/status");
                    JToken jtoken = JObject.Parse(trainerStatusJson);
                    int numLearnedExamples = (int)jtoken.SelectToken("Stage2_Learn_Total");
                    trainerStatus = $"Trainer OK. Total learned examples: {numLearnedExamples}";
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Unable to connect to the remote server"))
                {
                    trainerStatus = "Please wait as trainer has not started yet";
                }
                else
                {
                    new TelemetryClient().TrackException(ex);
                    trainerStatus = "Error getting trainer status, check Application Insights for more details.";
                }
            }

            return GetEvalData(windowType, maxNumPolicies, trainerStatus);
        }

        private ActionResult GetEvalData(string windowType, int maxNumPolicies, string trainerStatus = null)
        {
            try
            {
                var policyRegex = "Constant Policy (.*)";
                var regex = new Regex(policyRegex);

                var evalContainer = (CloudBlobContainer)Session[SKEvalContainer];
                if (!evalContainer.Exists())
                {
                    return Json(new { DataError = "No evaluation data detected", TrainerStatus = trainerStatus }, JsonRequestBehavior.AllowGet);
                }
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
                            if (!evalData.ContainsKey(evalResult.PolicyName))
                            {
                                evalData.Add(evalResult.PolicyName, new EvalD3 { key = evalResult.PolicyName, values = new Dictionary<DateTime, float>() });
                            }
                            var timeToReward = evalData[evalResult.PolicyName].values;
                            if (timeToReward.ContainsKey(evalResult.LastWindowTime))
                            {
                                timeToReward[evalResult.LastWindowTime] = -evalResult.AverageCost;
                            }
                            else
                            {
                                timeToReward.Add(evalResult.LastWindowTime, -evalResult.AverageCost);
                            }
                        }
                    }
                }

                var evalDataD3 = evalData.Values.Select(a => new { key = GetDemoPolicyName(a.key), values = a.values.Select(v => new object[] { v.Key, v.Value }) });

                return Json(new { Data = evalDataD3, TrainerStatus = trainerStatus, ModelUpdateTime = APIController.ModelUpdateTime }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                new TelemetryClient().TrackException(ex);

                return Json(new { DataError = "Unable to load evaluation result", TrainerStatus = trainerStatus, ModelUpdateTime = APIController.ModelUpdateTime }, JsonRequestBehavior.AllowGet);
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

        private static string GetDemoPolicyName(string policyName)
        {
            switch (policyName)
            {
                case "Constant Policy 1":
                    return "AI article (predicted)";
                case "Constant Policy 2":
                    return "Federal Reserve article (predicted)";
                case "Deployed Policy":
                    return "Current policy (actual)";
                case "Latest Policy":
                    return "Current policy (predicted)";
                default:
                    return policyName;
            }
        }

        private static string[] GetEvalFilterWindowTypes()
        {
            return new string[] { "1m", "1h", "1d", "6d" };
        }

        private SimulationViewModel SimulationView()
        {
            var clientSettingsBlob = (CloudBlockBlob)Session[SKClientSettingsBlob];
            var clientApp = JsonConvert.DeserializeObject<ApplicationClientMetadata>(clientSettingsBlob.DownloadText());

            return new SimulationViewModel
            {
                Password = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKPassword],
                WebServiceToken = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKWebServiceToken],
                TrainerToken = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKAdminToken],
                EvaluationView = new EvaluationViewModel { WindowFilters = new List<string>(GetEvalFilterWindowTypes()), SelectedFilter = DefaultEvalWindow },
                TrainerArguments = clientApp.TrainArguments
            };
        }

        private List<SettingItemViewModel> SettingsView()
        {
            var clientSettingsBlob = (CloudBlockBlob)Session[SKClientSettingsBlob];
            ApplicationClientMetadata clientApp = JsonConvert.DeserializeObject<ApplicationClientMetadata>(clientSettingsBlob.DownloadText());
            Session[SKClientSettings] = clientApp;

            var extraSettingsBlob = (CloudBlockBlob)Session[SKExtraSettingsBlob];
            ApplicationExtraMetadata extraApp = JsonConvert.DeserializeObject<ApplicationExtraMetadata>(extraSettingsBlob.DownloadText());
            Session[SKExtraSettings] = extraApp;

            return CreateAppView(clientApp, extraApp);
        }

        private List<SettingItemViewModel> CreateAppView(
            ApplicationClientMetadata clientMetadata,
            ApplicationExtraMetadata extraMetadata)
        {
            string uniqueStringInUrl = Regex.Match(Request.Url.ToString(), ".*mc-(.*).azurewebsites.*").Groups[1].Value;

            CollectiveSettingsView svm = CreateCollectiveSettings(clientMetadata, extraMetadata, uniqueStringInUrl);

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
                new { Name = nameof(svm.ApplicationInsightsInstrumentationKey), Tooltip = "View Application Logs", Url = $"https://ms.portal.azure.com/#blade/AppInsightsExtension/SearchBlade/ComponentId/%7B%22SubscriptionId%22%3A%22{svm.AzureSubscriptionId}%22%2C%22ResourceGroup%22%3A%22{svm.AzureResourceGroupName}%22%2C%22Name%22%3A%22{svm.AzureResourceGroupName}-appinsights-{uniqueStringInUrl}%22%7D/InitialFilter/%7B%22eventTypes%22%3A%5B4%2C1%2C3%2C5%2C2%2C6%5D%2C%22typeFacets%22%3A%7B%7D%2C%22isPermissive%22%3Afalse%7D/InitialTime/%7B%22durationMs%22%3A43200000%2C%22endTime%22%3Anull%2C%22isInitialTime%22%3Atrue%2C%22grain%22%3A1%7D/InitialQueryText//ConfigurationId/blankSearch%3A"},
                new { Name = nameof(svm.ASAJoinName), Tooltip = "View ASA Join Query", Url = $"https://ms.portal.azure.com/#resource/subscriptions/{svm.AzureSubscriptionId}/resourceGroups/{svm.AzureResourceGroupName}/providers/Microsoft.StreamAnalytics/streamingjobs/{svm.ASAJoinName}"},
                new { Name = nameof(svm.ASAEvalName), Tooltip = "View ASA Policy Evaluation Query", Url = $"https://ms.portal.azure.com/#resource/subscriptions/{svm.AzureSubscriptionId}/resourceGroups/{svm.AzureResourceGroupName}/providers/Microsoft.StreamAnalytics/streamingjobs/{svm.ASAEvalName}"},
                new { Name = nameof(svm.OnlineTrainerAddress), Tooltip = "Configure Online Trainer", Url = $"https://manage.windowsazure.com/microsoft.onmicrosoft.com#Workspaces/CloudServicesExtension/CloudService/{svm.OnlineTrainerAddress}/configure"}
            };
            var nameToEditable = new[]
            {
                new { Name = nameof(svm.TrainArguments), IsEditable = true },
                new { Name = nameof(svm.IsExplorationEnabled), IsEditable = true },
                new { Name = nameof(svm.InitialExplorationEpsilon), IsEditable = true },
                new { Name = nameof(svm.SelectedModelId), IsEditable = true }
            };
            var nameToSpotLightUrl = new[]
            {
                new { Name = nameof(svm.AzureResourceGroupName), IsSplotlightUrl = true }
            };
            var nameToResettable = new[]
            {
                new { Name = nameof(svm.OnlineTrainerAddress), Resettable = true }
            };
            var settingItems = from nv in nameToValue
                        from nht in nameToHelpText.Where(n => n.Name == nv.Name).DefaultIfEmpty()
                        from nu in nameToUrl.Where(n => n.Name == nv.Name).DefaultIfEmpty()
                        from ne in nameToEditable.Where(n => n.Name == nv.Name).DefaultIfEmpty()
                        from nsl in nameToSpotLightUrl.Where(n => n.Name == nv.Name).DefaultIfEmpty()
                        from nrs in nameToResettable.Where(n => n.Name == nv.Name).DefaultIfEmpty()
                        select new SettingItemViewModel
                        {
                            Value = nv.Value,
                            DisplayName = nht?.HelpText[0],
                            HelpText = nht?.HelpText[1],
                            IsEditable = ne?.IsEditable,
                            Name = nv.Name,
                            Url = nu?.Url,
                            UrlToolTip = nu?.Tooltip,
                            IsSplotlightUrl = nsl?.IsSplotlightUrl,
                            IsResettable = nrs?.Resettable
                        };

            return settingItems.ToList();
        }

        private CollectiveSettingsView CreateCollectiveSettings(ApplicationClientMetadata clientMetadata, ApplicationExtraMetadata extraMetadata, string uniqueStringInUrl)
        {
            var svm = new CollectiveSettingsView
            {
                ApplicationId = clientMetadata.ApplicationID,
                AzureSubscriptionId = extraMetadata.SubscriptionId,
                DecisionType = extraMetadata.DecisionType,
                TrainFrequency = extraMetadata.TrainFrequency,
                TrainArguments = clientMetadata.TrainArguments,
                AzureStorageConnectionString = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString],
                AzureResourceGroupName = extraMetadata.AzureResourceGroupName,
                ApplicationInsightsInstrumentationKey = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKAppInsightsKey],
                OnlineTrainerAddress = ConfigurationManager.AppSettings[ApplicationMetadataStore.AKTrainerURL],
                WebApiAddress = $"https://{extraMetadata.AzureResourceGroupName}-webapi-{uniqueStringInUrl}.azurewebsites.net",
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
                InitialExplorationEpsilon = clientMetadata.InitialExplorationEpsilon,
                SettingsBlobUri = extraMetadata.SettingsTokenUri1
            };
            return svm;
        }
    }
}