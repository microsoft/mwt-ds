using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiWorldTesting;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;

namespace DecisionSample
{
    interface IExploreAlgorithm { }
    class EpsilonGreedy : IExploreAlgorithm
    {
        public EpsilonGreedy(float epsilon, uint numActions)
        {
            Epsilon = epsilon;
            NumActions = numActions;
        }

        public float Epsilon { get; set; }
        public uint NumActions { get; set; }
    }

    /// <summary>
    /// Configuration object for the client decision service which contains settings for batching, retry storage, etc...
    /// </summary>
    class DecisionServiceConfiguration<TContext>
    {
        public DecisionServiceConfiguration()
        {
            ContextJsonSerializer = x => JsonConvert.SerializeObject(x);

            // Default configuration for batching
            BatchConfig = new BatchingConfiguration()
            {
                BufferSize = 4 * 1024 * 1024,
                Duration = TimeSpan.FromMinutes(1),
                EventCount = 10000
            };
        }
        public Func<TContext, string> ContextJsonSerializer { get; set; }
        public BatchingConfiguration BatchConfig { get; set; }
    }

    /// <summary>
    /// Encapsulates logic for recorder with async server communications & policy update.
    /// </summary>
    class DecisionService<TContext> : IDisposable
    {
        public DecisionService(string appId, DecisionServiceConfiguration<TContext> config)
        {
            recorder = new DecisionServiceRecorder<TContext>(config.BatchConfig);
            policy = new DecisionServicePolicy<TContext>();
            mwt = new MwtExplorer<TContext>(appId, recorder);
        }

        /*ReportSimpleReward*/
        public void ReportReward(float reward, string uniqueKey)
        {
            //recorder.ReportOutcome(outcomeJson, reward, uniqueKey);
        }

        public void ReportOutcome(string outcomeJson, string uniqueKey)
        {
            //recorder.ReportOutcome(outcomeJson, reward, uniqueKey);
        }

        public uint ChooseAction(IExplorer<TContext> explorer, string uniqueKey, TContext context)
        {
            return mwt.ChooseAction(explorer, uniqueKey, context);
        }

        public void Dispose() { }

        public IRecorder<TContext> Recorder { get { return recorder; } }
        public IPolicy<TContext> Policy { get { return policy; } }

        private DecisionServiceRecorder<TContext> recorder;
        private DecisionServicePolicy<TContext> policy;
        private MwtExplorer<TContext> mwt;
    }

    /// <summary>
    /// Represents a collection of batching criteria.  
    /// </summary>
    /// <remarks>
    /// A batch is created whenever a criterion is met.
    /// </remarks>
    class BatchingConfiguration
    {
        /// <summary>
        /// Period of time where events are grouped in one batch.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Maximum number of events in a batch.
        /// </summary>
        public uint EventCount { get; set; }

        /// <summary>
        /// Maximum size (in bytes) of a batch.
        /// </summary>
        public ulong BufferSize { get; set; }
    }

    // TODO: rename Recorder to Logger?
    // TODO: Client can tag event as interaction or observation
    internal class DecisionServiceRecorder<TContext> : IRecorder<TContext>, IDisposable
    {
        public DecisionServiceRecorder(BatchingConfiguration batchConfig) { }

        public void Record(TContext context, uint action, float probability, string uniqueKey) 
        {
            string contextJson = JsonConvert.SerializeObject(context);
            // TODO: at the time of server communication, if the client is out of memory (or meets some predefined upper bound):
            // 1. It can block the execution flow.
            // 2. Or drop events.
        }

        // TODO: should this also take a float reward?
        public void ReportOutcome(string outcomeJson, float? reward, string uniqueKey)
        {
            // . . .
        }

        // Internally, background tasks can get back latest model version as a return value from the HTTP communication with Ingress worker

        public void Dispose() { }
    }

    internal class DecisionServicePolicy<TContext> : IPolicy<TContext>, IDisposable
    { 
        // Recorder should talk to the Policy to pass over latest model version
        public uint ChooseAction(TContext context)
        {
            return 0;
        }

        public void Dispose() { }
    }

    /// <summary>
    /// TODO: separate object to handle the model communication with server?
    /// </summary>
    class ModelManager
    { 
        public void FindUpdateModel();
        public void UpdateModel();
    }
}
