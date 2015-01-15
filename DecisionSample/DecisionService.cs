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
        public string AppId { get; set; }
        public string AuthorizationToken { get; set; }
        public IExploreAlgorithm<TContext> Explorer { get; set; }
        public bool IsPolicyUpdatable { get; set; }
        public BatchingConfiguration BatchConfig { get; set; }
        public Func<TContext, string> ContextJsonSerializer { get; set; }
    }

    /// <summary>
    /// Encapsulates logic for recorder with async server communications & policy update.
    /// </summary>
    class DecisionService<TContext> : IDisposable
    {
        public DecisionService(DecisionServiceConfiguration<TContext> config)
        {
            recorder = new DecisionServiceRecorder<TContext>(config.BatchConfig);
            policy = new DecisionServicePolicy<TContext>();
            mwt = new MwtExplorer<TContext>(config.AppId, recorder);
            exploreAlgorithm = config.Explorer;
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

        public uint ChooseAction(string uniqueKey, TContext context)
        {
            return mwt.ChooseAction(exploreAlgorithm.Get(), uniqueKey, context);
        }

        public void Dispose() { }

        public IRecorder<TContext> Recorder { get { return recorder; } }
        public IPolicy<TContext> Policy { get { return policy; } }
            
        private IExploreAlgorithm<TContext> exploreAlgorithm;
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

    /* Temp classes to support interface */

    interface IExploreAlgorithm<TContext>
    {
        IExplorer<TContext> Get();
    }
    class EpsilonGreedyExplorer<TContext> : IExploreAlgorithm<TContext>
    {
        public EpsilonGreedyExplorer(IPolicy<TContext> policy, float epsilon, uint numActions)
        {
            Epsilon = epsilon;
            NumActions = numActions;
            Policy = policy;
        }

        public IPolicy<TContext> Policy { get; set; }
        public float Epsilon { get; set; }
        public uint NumActions { get; set; }

        public IExplorer<TContext> Get()
        {
            throw new NotImplementedException();
        }
    }

    class TauFirstExplorer<TContext> : IExploreAlgorithm<TContext>
    {
        public TauFirstExplorer(IPolicy<TContext> policy, uint tau, uint numActions)
        {
            Tau = tau;
            NumActions = numActions;
            Policy = policy;
        }

        public IPolicy<TContext> Policy { get; set; }
        public uint Tau { get; set; }
        public uint NumActions { get; set; }

        public IExplorer<TContext> Get()
        {
            throw new NotImplementedException();
        }
    }

    class BootstrapExplorer<TContext> : IExploreAlgorithm<TContext>
    {
        public BootstrapExplorer(IPolicy<TContext>[] policies, uint numActions)
        {
            NumActions = numActions;
            Policies = policies;
        }

        public IPolicy<TContext>[] Policies { get; set; }
        public uint NumActions { get; set; }

        public IExplorer<TContext> Get()
        {
            throw new NotImplementedException();
        }
    }

    class SoftmaxExplorer<TContext> : IExploreAlgorithm<TContext>
    {
        public SoftmaxExplorer(IScorer<TContext> scorer, float lambda, uint numActions)
        {
            Lambda = lambda;
            NumActions = numActions;
            Scorer = scorer;
        }

        public IScorer<TContext> Scorer { get; set; }
        public float Lambda { get; set; }
        public uint NumActions { get; set; }

        public IExplorer<TContext> Get()
        {
            throw new NotImplementedException();
        }
    }

    class GenericExplorer<TContext> : IExploreAlgorithm<TContext>
    {
        public GenericExplorer(IScorer<TContext> scorer, uint numActions)
        {
            NumActions = numActions;
            Scorer = scorer;
        }

        public IScorer<TContext> Scorer { get; set; }
        public uint NumActions { get; set; }

        public IExplorer<TContext> Get()
        {
            throw new NotImplementedException();
        }
    }
}
