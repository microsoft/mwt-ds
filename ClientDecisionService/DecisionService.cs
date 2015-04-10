using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using Microsoft.Research.DecisionService.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using MultiWorldTesting;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ClientDecisionService
{
    /// <summary>
    /// Encapsulates logic for recorder with async server communications & policy update.
    /// </summary>
    public class DecisionService<TContext> : IDisposable
    {
        public DecisionService(DecisionServiceConfiguration<TContext> config)
        {
            this.DownloadSettings();

            this.blobUpdater = new AzureBlobUpdater(this.UpdateSettings, 
                "settings", 
                this.applicationSettingsBlobUri, 
                this.applicationConnectionString, 
                config.PolicyModelOutputDir);

            recorder = new DecisionServiceRecorder<TContext>(
                config.BatchConfig, 
                config.ContextJsonSerializer,
                config.AuthorizationToken);

            policy = new DecisionServicePolicy<TContext>(UpdatePolicy, 
                this.applicationModelBlobUri, this.applicationConnectionString,
                config.PolicyModelOutputDir);

            mwt = new MwtExplorer<TContext>(config.AuthorizationToken, recorder);
            explorer = config.Explorer;
        }

        /*ReportSimpleReward*/
        public void ReportReward(float reward, string uniqueKey)
        {
            recorder.ReportReward(reward, uniqueKey);
        }

        public bool TryReportReward(float reward, string uniqueKey)
        {
            return recorder.TryReportReward(reward, uniqueKey);
        }

        public void ReportOutcome(string outcomeJson, string uniqueKey)
        {
            recorder.ReportOutcome(outcomeJson, uniqueKey);
        }

        public bool TryReportOutcome(string outcomeJson, string uniqueKey)
        {
            return recorder.TryReportOutcome(outcomeJson, uniqueKey);
        }

        public uint ChooseAction(string uniqueKey, TContext context)
        {
            return mwt.ChooseAction(explorer, uniqueKey, context);
        }

        public void Flush()
        {
            blobUpdater.StopPolling();
            policy.StopPolling();
            recorder.Flush();
        }

        public void Dispose() { }

        private void DownloadSettings()
        {
            var retryStrategy = new ExponentialBackoff(DecisionServiceConstants.RetryCount,
                    DecisionServiceConstants.RetryMinBackoff, DecisionServiceConstants.RetryMaxBackoff, DecisionServiceConstants.RetryDeltaBackoff);

            RetryPolicy retryPolicy = new RetryPolicy<DecisionServiceTransientErrorDetectionStrategy>(retryStrategy);

            string metadataJson = retryPolicy.ExecuteAction(() =>
            {
                WebClient wc = new WebClient();
                return wc.DownloadString(DecisionServiceConstants.CommandCenterAddress + DecisionServiceConstants.MetadataAddress);
            });

            if (String.IsNullOrEmpty(metadataJson))
            {
                throw new Exception("Unable to download application settings from control center.");
            }

            var metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(metadataJson);
            this.applicationConnectionString = metadata.ConnectionString;
            this.applicationSettingsBlobUri = metadata.SettingsBlobUri;
            this.applicationModelBlobUri = metadata.ModelBlobUri;

            this.ToggleExploration(metadata.IsExplorationEnabled);
        }

        private void UpdateSettings(string settingsFile)
        {
            string metadataJson = File.ReadAllText(settingsFile);
            var metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(metadataJson);

            this.ToggleExploration(metadata.IsExplorationEnabled);
        }

        private void ToggleExploration(bool explore)
        {
            // TODO: Turn off exploration here
        }

        private void UpdatePolicy()
        {
            IConsumePolicy<TContext> consumePolicy = explorer as IConsumePolicy<TContext>;
            if (consumePolicy != null)
            {
                consumePolicy.UpdatePolicy(policy);
                Trace.TraceInformation("Model update succeeded.");
            }
            else
            {
                // TODO: how to handle updating policies for Bootstrap explorers?
                throw new NotSupportedException("This type of explorer does not currently support updating policy functions.");
            }
        }

        public IRecorder<TContext> Recorder { get { return recorder; } }
        public IPolicy<TContext> Policy { get { return policy; } }

        AzureBlobUpdater blobUpdater;

        private string applicationConnectionString;
        private string applicationSettingsBlobUri;
        private string applicationModelBlobUri;

        private readonly IExplorer<TContext> explorer;
        private readonly DecisionServiceRecorder<TContext> recorder;
        private readonly DecisionServicePolicy<TContext> policy;
        private readonly MwtExplorer<TContext> mwt;
    }
}
