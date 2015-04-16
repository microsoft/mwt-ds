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
            explorer = config.Explorer;

            logger = config.Logger ?? new DecisionServiceLogger<TContext>(
                config.BatchConfig, 
                config.ContextJsonSerializer,
                config.AuthorizationToken,
                config.LoggingServiceAddress);

            mwt = new MwtExplorer<TContext>(config.AuthorizationToken, logger);

            if (!config.OfflineMode)
            {
                this.commandCenterBaseAddress = config.CommandCenterAddress ?? DecisionServiceConstants.CommandCenterAddress;
                this.DownloadSettings(config.AuthorizationToken);

                this.blobUpdater = new AzureBlobUpdater(this.UpdateSettings,
                    "settings",
                    this.applicationSettingsBlobUri,
                    this.applicationConnectionString,
                    config.BlobOutputDir);

                policy = new DecisionServicePolicy<TContext>(UpdatePolicy,
                    this.applicationModelBlobUri, this.applicationConnectionString,
                    config.BlobOutputDir);
            }
        }

        /*ReportSimpleReward*/
        public void ReportReward(float reward, string uniqueKey)
        {
            logger.ReportReward(reward, uniqueKey);
        }

        public void ReportOutcome(string outcomeJson, string uniqueKey)
        {
            logger.ReportOutcome(outcomeJson, uniqueKey);
        }

        public uint ChooseAction(string uniqueKey, TContext context)
        {
            return mwt.ChooseAction(explorer, uniqueKey, context);
        }

        public void Flush()
        {
            if (blobUpdater != null)
            {
                blobUpdater.StopPolling();
            }

            if (policy != null)
            {
                policy.StopPolling();
            }

            if (logger != null)
            {
                logger.Flush();
            }
        }

        public void Dispose() { }

        private void DownloadSettings(string token)
        {
            var retryStrategy = new ExponentialBackoff(DecisionServiceConstants.RetryCount,
                DecisionServiceConstants.RetryMinBackoff, DecisionServiceConstants.RetryMaxBackoff, DecisionServiceConstants.RetryDeltaBackoff);

            RetryPolicy retryPolicy = new RetryPolicy<DecisionServiceTransientErrorDetectionStrategy>(retryStrategy);

            string metadataJson = retryPolicy.ExecuteAction(() =>
            {
                WebClient wc = new WebClient();
                return wc.DownloadString(string.Format(this.commandCenterBaseAddress + DecisionServiceConstants.MetadataAddress, token));
            });

            if (String.IsNullOrEmpty(metadataJson))
            {
                throw new Exception("Unable to download application settings from control center.");
            }

            var metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(metadataJson);
            this.applicationConnectionString = metadata.ConnectionString;
            this.applicationSettingsBlobUri = metadata.SettingsBlobUri;
            this.applicationModelBlobUri = metadata.ModelBlobUri;

            this.explorer.EnableExplore(metadata.IsExplorationEnabled);
        }

        private void UpdateSettings(string settingsFile)
        {
            string metadataJson = File.ReadAllText(settingsFile);
            var metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(metadataJson);

            this.explorer.EnableExplore(metadata.IsExplorationEnabled);
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

        public IRecorder<TContext> Recorder { get { return logger; } }
        public IPolicy<TContext> Policy { get { return policy; } }

        private readonly string commandCenterBaseAddress;

        AzureBlobUpdater blobUpdater;

        private string applicationConnectionString;
        private string applicationSettingsBlobUri;
        private string applicationModelBlobUri;

        private readonly IExplorer<TContext> explorer;
        private readonly ILogger<TContext> logger;
        private readonly DecisionServicePolicy<TContext> policy;
        private readonly MwtExplorer<TContext> mwt;
    }
}
