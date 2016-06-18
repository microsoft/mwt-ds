using DecisionServicePrivateWeb.Classes;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Web.Mvc;
using VW;
using VW.Serializer;

namespace DecisionServicePrivateWeb.Controllers
{
    //public class VowpalWabbitValidationController : ApiBaseController
    //{
    //    private ApplicationClientMetadata metaData;
    //    private DateTime lastDownload;

    //    [HttpPost]
    //    public ActionResult Post()
    //    {
    //        this.Authenticate();

    //        if (this.metaData == null || lastDownload + TimeSpan.FromMinutes(1) < DateTime.Now)
    //        {
    //            // TODO: use session
    //            var storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings[ApplicationMetadataStore.AKConnectionString]);
    //            var blobClient = storageAccount.CreateCloudBlobClient();
    //            var settingsBlobContainer = blobClient.GetContainerReference(ApplicationBlobConstants.SettingsContainerName);
    //            var clientSettingsBlob = settingsBlobContainer.GetBlockBlobReference(ApplicationBlobConstants.LatestClientSettingsBlobName);

    //            this.metaData = JsonConvert.DeserializeObject<ApplicationClientMetadata>(clientSettingsBlob.DownloadText());
    //            lastDownload = DateTime.Now;
    //        }

    //        var context = this.ReadBody();

    //        using (var vw = new VowpalWabbit(new VowpalWabbitSettings(metaData.TrainArguments)
    //        {
    //            EnableStringExampleGeneration = true,
    //            EnableStringFloatCompact = true
    //        }))
    //        using (var serializer = new VowpalWabbitJsonSerializer(vw))
    //        using (var example = serializer.ParseAndCreate(context))
    //        {
    //            return Json(example.VowpalWabbitString);
    //        }
    //    }
    //}
}