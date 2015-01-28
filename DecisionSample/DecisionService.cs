using MultiWorldTesting;
using System;
using System.Threading.Tasks;

namespace DecisionSample
{
    /// <summary>
    /// Encapsulates logic for recorder with async server communications & policy update.
    /// </summary>
    public class DecisionService<TContext> : IDisposable
    {
        public DecisionService(DecisionServiceConfiguration<TContext> config)
        {
            recorder = new DecisionServiceRecorder<TContext>(config.BatchConfig, config.ContextJsonSerializer,
                config.ExperimentalUnitDurationInSeconds, config.AuthorizationToken);
            policy = new DecisionServicePolicy<TContext>();
            mwt = new MwtExplorer<TContext>(config.AppId, recorder);
            exploreAlgorithm = config.Explorer;
        }

        /*ReportSimpleReward*/
        public void ReportReward(float reward, string uniqueKey)
        {
            recorder.ReportReward(reward, uniqueKey);
        }

        public void ReportOutcome(string outcomeJson, string uniqueKey)
        {
            recorder.ReportOutcome(outcomeJson, uniqueKey);
        }

        public uint ChooseAction(string uniqueKey, TContext context)
        {
            return mwt.ChooseAction(exploreAlgorithm.Get(), uniqueKey, context);
        }

        public async Task FlushAsync()
        { 
            await recorder.FlushAsync();
        }

        public void Dispose() { }

        public IRecorder<TContext> Recorder { get { return recorder; } }
        public IPolicy<TContext> Policy { get { return policy; } }
            
        private IExploreAlgorithm<TContext> exploreAlgorithm;
        private DecisionServiceRecorder<TContext> recorder;
        private DecisionServicePolicy<TContext> policy;
        private MwtExplorer<TContext> mwt;
    }
}
