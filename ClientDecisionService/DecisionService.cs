using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using Microsoft.Research.MultiWorldTesting.Contract;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using MultiWorldTesting;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientDecisionService
{
    /// <summary>
    /// Encapsulates logic for recorder with async server communications & policy update.
    /// </summary>
    public class DecisionService<TContext> : IDisposable
    {
        /// <summary>
        /// Construct a <see cref="DecisionService{TContext}"/> object with the specified <see cref="DecisionServiceConfiguration{TContext}"/> configuration.
        /// </summary>
        /// <param name="config">The configuration object.</param>
        public DecisionService(DecisionServiceConfiguration<TContext> config)
        {
            explorer = config.Explorer;

            if (!config.OfflineMode)
            {
                this.recorder = config.Recorder ?? new JoinServiceLogger<TContext>(
                    config.JoinServiceBatchConfiguration,
                    config.ContextJsonSerializer,
                    config.AuthorizationToken,
                    config.LoggingServiceAddress);

                string serviceConnectionString = config.ServiceAzureStorageConnectionString ?? DecisionServiceConstants.MwtServiceAzureStorageConnectionString;
                ApplicationTransferMetadata metadata = this.GetBlobLocations(config.AuthorizationToken, serviceConnectionString);

                this.settingsBlobPollDelay = config.PollingForSettingsPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForSettingsPeriod;
                this.modelBlobPollDelay = config.PollingForModelPeriod == TimeSpan.Zero ? DecisionServiceConstants.PollDelay : config.PollingForModelPeriod;

                if (this.settingsBlobPollDelay != TimeSpan.MinValue)
                {
                    this.blobUpdater = new AzureBlobUpdater(
                        "settings",
                        metadata.SettingsBlobUri,
                        metadata.ConnectionString,
                        config.BlobOutputDir,
                        this.settingsBlobPollDelay,
                        this.UpdateSettings,
                        config.SettingsPollFailureCallback);
                }
                
                this.policy = new DecisionServicePolicy<TContext>(
                    metadata.ModelBlobUri, metadata.ConnectionString,
                    config.BlobOutputDir,
                    this.modelBlobPollDelay,
                    this.InternalPolicyUpdated,
                    config.ModelPollFailureCallback);
            }
            else
            {
                this.recorder = config.Recorder;
                if (this.recorder == null)
                {
                    throw new ArgumentException("A custom recorder must be defined when operating in offline mode.", "Recorder");
                }
            }

            mwt = new MwtExplorer<TContext>(config.AuthorizationToken, this.recorder);
        }

        /// <summary>
        /// Report a simple float reward for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="reward">The simple float reward.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        public void ReportReward(float reward, string uniqueKey)
        {
            ILogger<TContext> logger = this.recorder as ILogger<TContext>;
            if (logger != null)
            {
                logger.ReportReward(reward, uniqueKey);
            }
        }

        /// <summary>
        /// Report an outcome in JSON format for the experimental unit identified by the given unique key.
        /// </summary>
        /// <param name="outcomeJson">The outcome object in JSON format.</param>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        /// <remarks>
        /// Outcomes are general forms of observations that can be converted to simple float rewards as required by some ML algorithms for optimization.
        /// </remarks>
        public void ReportOutcome(string outcomeJson, string uniqueKey)
        {
            ILogger<TContext> logger = this.recorder as ILogger<TContext>;
            if (logger != null)
            {
                logger.ReportOutcome(outcomeJson, uniqueKey);
            }
        }

        /// <summary>
        /// Performs explore-exploit to choose a list of actions based on the specified context.
        /// </summary>
        /// <param name="uniqueKey">The unique key of the experimental unit.</param>
        /// <param name="context">The context for this interaction.</param>
        /// <returns>An array containing the chosen actions.</returns>
        /// <remarks>
        /// This method will send logging data to the <see cref="IRecorder{TContext}"/> object specified at initialization.
        /// </remarks>
        public uint[] ChooseAction(string uniqueKey, TContext context)
        {
            return mwt.ChooseAction(explorer, uniqueKey, context);
        }

        /// <summary>
        /// Update decision service with a new <see cref="IPolicy{TContext}"/> object that will be used by the exploration algorithm.
        /// </summary>
        /// <param name="newPolicy">The new <see cref="IPolicy{TContext}"/> object.</param>
        public void UpdatePolicy(IPolicy<TContext> newPolicy)
        {
            UpdateInternalPolicy(newPolicy);
        }

        /// <summary>
        /// Flush any pending data to be logged and request to stop all polling as appropriate.
        /// </summary>
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

            ILogger<TContext> logger = this.recorder as ILogger<TContext>;
            if (logger != null)
            {
                logger.Flush();
            }
        }

        public void Dispose() { }

        private ApplicationTransferMetadata GetBlobLocations(string token, string serviceAzureStorageConnectionString)
        {
            ApplicationTransferMetadata metadata = null;
            CloudStorageAccount storageAccount = null;

            bool accountFound = CloudStorageAccount.TryParse(serviceAzureStorageConnectionString, out storageAccount);
            if (!accountFound || storageAccount == null)
            {
                throw new Exception("Could not connect to Azure storage for the service.");
            }

            try
            {
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                blobClient.DefaultRequestOptions.RetryPolicy = new ExponentialRetry(DecisionServiceConstants.RetryDeltaBackoff, DecisionServiceConstants.RetryCount);

                CloudBlobContainer settingsContainer = blobClient.GetContainerReference(DecisionServiceConstants.ApplicationBlobLocationContainerName);
                CloudBlockBlob settingsBlob = settingsContainer.GetBlockBlobReference(token);

                using (var ms = new MemoryStream())
                {
                    settingsBlob.DownloadToStream(ms);
                    metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
            catch (Exception ex)
            {
                var storageException = ex as StorageException;
                if (storageException != null)
                {
                    if (storageException.RequestInformation.HttpStatusCode == 404)
                    { 
                        throw new StorageException("Unable to retrieve blob locations from storage using the specified token", ex.InnerException);
                    }
                }
                throw;
            }
            return metadata;
        }

        private void UpdateSettings(string settingsFile)
        {
            try
            {
                string metadataJson = File.ReadAllText(settingsFile);
                var metadata = JsonConvert.DeserializeObject<ApplicationTransferMetadata>(metadataJson);

                this.explorer.EnableExplore(metadata.IsExplorationEnabled);
            }
            catch (Exception ex)
            {
                if (ex is JsonReaderException)
                {
                    Trace.TraceWarning("Cannot read new settings.");
                }
                else
                {
                    throw;
                }
            }
            
        }

        private void InternalPolicyUpdated()
        {
            UpdateInternalPolicy(policy);
        }

        private void UpdateInternalPolicy(IPolicy<TContext> newPolicy)
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

        private readonly TimeSpan settingsBlobPollDelay;
        private readonly TimeSpan modelBlobPollDelay;

        AzureBlobUpdater blobUpdater;

        private readonly IExplorer<TContext> explorer;
        private readonly IRecorder<TContext> recorder;
        private readonly DecisionServicePolicy<TContext> policy;
        private readonly MwtExplorer<TContext> mwt;
    }
}
