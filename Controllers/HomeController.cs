using DecisionServicePrivateWeb.Classes;
using DecisionServicePrivateWeb.Models;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
    public class HomeController : Controller
    {
        const string AKConnectionString = "AzureStorageConnectionString";
        const string AKPassword = "Password";
        const string SKAuthenticated = "Authenticated";

        const string SKClientSettingsBlob = "ClientSettingsBlob";
        const string SKTrainerSettingsBlob = "TrainerSettingsBlob";
        const string SKExtraSettingsBlob = "ExtraSettingsBlob";
        const string SKEvalContainer = "EvalContainer";

        const string SKClientSettings = "ClientSettings";
        const string SKTrainerSettings = "TrainerSettings";
        const string SKExtraSettings = "ExtraSettings";

        public ActionResult Index()
        {
            return View(new IndexViewModel { Authenticated = IsAuthenticated(Session) });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index(string password)
        {
            string correctPassword = ConfigurationManager.AppSettings[AKPassword];
            if (string.Equals(password, correctPassword))
            {
                Session[SKAuthenticated] = true;

                string azureStorageConnectionString = ConfigurationManager.AppSettings[AKConnectionString];
                var storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
                var blobClient = storageAccount.CreateCloudBlobClient();
                var blobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.SettingsContainerName);

                Session[SKClientSettingsBlob] = blobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestClientSettingsBlobName);
                Session[SKTrainerSettingsBlob] = blobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestTrainerSettingsBlobName);
                Session[SKExtraSettingsBlob] = blobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestExtraSettingsBlobName);

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

        [AllowAnonymous]
        public ActionResult Settings()
        {
            if (!IsAuthenticated(Session))
            {
                return RedirectToAction("Index");
            }
            string userName = User.Identity.Name;
            ApplicationClientMetadata clientApp = null;
            ApplicationTrainerMetadata trainerApp = null;
            ApplicationExtraMetadata extraApp = null;
            try
            {
                var clientSettingsBlob = (CloudBlockBlob)Session[SKClientSettingsBlob];
                clientApp = JsonConvert.DeserializeObject<ApplicationClientMetadata>(clientSettingsBlob.DownloadText());
                Session[SKClientSettings] = clientApp;

                var trainerSettingsBlob = (CloudBlockBlob)Session[SKTrainerSettingsBlob];
                trainerApp = JsonConvert.DeserializeObject<ApplicationTrainerMetadata>(trainerSettingsBlob.DownloadText());
                Session[SKTrainerSettings] = trainerApp;

                var extraSettingsBlob = (CloudBlockBlob)Session[SKExtraSettingsBlob];
                extraApp = JsonConvert.DeserializeObject<ApplicationExtraMetadata>(extraSettingsBlob.DownloadText());
                Session[SKExtraSettings] = extraApp;
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to load application metadata: {ex.ToString()}");
            }

            return View(CreateAppView(clientApp, trainerApp, extraApp));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Settings(SettingsSaveModel model)
        {
            try
            {
                var clientMeta = (ApplicationClientMetadata)Session[SKClientSettings];
                clientMeta.IsExplorationEnabled = model.IsExplorationEnabled;
                var clientSettingsBlob = (CloudBlockBlob)Session[SKClientSettingsBlob];
                clientSettingsBlob.UploadText(JsonConvert.SerializeObject(clientMeta));

                var trainerMeta = (ApplicationTrainerMetadata)Session[SKTrainerSettings];
                trainerMeta.AdditionalTrainArguments = model.AdditionalTrainArguments;
                var trainerSettingsBlob = (CloudBlockBlob)Session[SKTrainerSettingsBlob];
                trainerSettingsBlob.UploadText(JsonConvert.SerializeObject(trainerMeta));

                var extraMeta = (ApplicationExtraMetadata)Session[SKExtraSettings];
                extraMeta.ModelId = model.SelectedModelId;
                var extraSettingsBlob = (CloudBlockBlob)Session[SKExtraSettingsBlob];
                extraSettingsBlob.UploadText(JsonConvert.SerializeObject(extraMeta));

                try
                {
                    // copy selected model file to the latest file
                    string azureStorageConnectionString = ConfigurationManager.AppSettings[AKConnectionString];
                    var storageAccount = CloudStorageAccount.Parse(azureStorageConnectionString);
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    var modelContainer = blobClient.GetContainerReference(ApplicationBlobConstants.ModelContainerName);
                    var selectedModelBlob = this.GetSelectedModelBlob(modelContainer, model.SelectedModelId);
                    if (selectedModelBlob != null && modelContainer != null)
                    {
                        var currentModelBlob = modelContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestModelBlobName);
                        currentModelBlob.StartCopy(selectedModelBlob);
                    }
                }
                catch (Exception ex)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to update model: {ex.ToString()}");
                }

                return View(CreateAppView(clientMeta, trainerMeta, extraMeta));
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError, $"Unable to update application metadata: {ex.ToString()}");
            }
        }

        [AllowAnonymous]
        public ActionResult Evaluation()
        {
            if (!IsAuthenticated(Session))
            {
                return RedirectToAction("Index");
            }

            return View(new EvaluationViewModel { WindowFilters = new List<string>(new string[] { "1h", "3h", "6h" }), SelectedFilter = "3h" });
        }

        [AllowAnonymous]
        public ActionResult EvalJson(string windowType = "3h", int maxNumPolicies = 5)
        {
            var policyRegex = "Policy (.*)";
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
                            if (Convert.ToInt32(regex.Match(evalResult.PolicyName).Groups[1].Value) > maxNumPolicies)
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

        private CloudBlockBlob GetSelectedModelBlob(CloudBlobContainer blobContainer, string modelId, bool forceLatest = false)
        {
            CloudBlockBlob blockBlob = null;

            bool useLatestModel = forceLatest | String.Equals(modelId, ApplicationSettingConstants.UseLatestModelSetting, StringComparison.OrdinalIgnoreCase);

            if (!useLatestModel) // If not latest, use the selected model
            {
                if (!String.IsNullOrWhiteSpace(modelId))
                {
                    blockBlob = blobContainer.GetBlockBlobReference(modelId);
                }
            }
            else
            {
                DateTimeOffset lastBlobDate = new DateTimeOffset();
                IEnumerable<IListBlobItem> blobs = blobContainer.ListBlobs();
                foreach (IListBlobItem blobItem in blobs)
                {
                    if (blobItem is CloudBlockBlob && !IsLatestModelBlob(blobItem))
                    {
                        var bbItem = (CloudBlockBlob)blobItem;
                        DateTimeOffset bbDate = bbItem.Properties.LastModified.GetValueOrDefault();
                        if (bbDate >= lastBlobDate)
                        {
                            blockBlob = bbItem;
                            lastBlobDate = bbDate;
                        }
                    }
                }
            }

            return blockBlob;
        }

        private static bool IsLatestModelBlob(IListBlobItem blob)
        {
            return blob is CloudBlockBlob && string.Compare(((CloudBlockBlob)blob).Name, ApplicationBlobConstants.LatestModelBlobName, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static SettingsViewModel CreateAppView(
            ApplicationClientMetadata clientMetadata,
            ApplicationTrainerMetadata trainerMetadata,
            ApplicationExtraMetadata extraMetadata)
        {
            var svm = new SettingsViewModel
            {
                ApplicationId = clientMetadata.ApplicationID,
                AzureSubscriptionId = extraMetadata.SubscriptionId,
                DecisionType = extraMetadata.DecisionType,
                NumActions = clientMetadata.NumActions,
                TrainFrequency = extraMetadata.TrainFrequency,
                AdditionalTrainArguments = trainerMetadata.AdditionalTrainArguments,
                AzureStorageConnectionString = trainerMetadata.ConnectionString,
                AzureResourceGroupName = extraMetadata.AzureResourceGroupName,
                EventHubInteractionConnectionString = clientMetadata.EventHubInteractionConnectionString,
                EventHubObservationConnectionString = clientMetadata.EventHubObservationConnectionString,
                ExperimentalUnitDuration = extraMetadata.ExperimentalUnitDuration,
                ModelIdList = new List<BlobModelViewModel>(),
                SelectedModelId = extraMetadata.ModelId,
                IsExplorationEnabled = clientMetadata.IsExplorationEnabled
            };
            svm.ModelIdList.Add(new BlobModelViewModel { Name = "Latest" });
            return svm;
        }

    }
}