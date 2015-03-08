using MultiWorldTesting;
using System;
using System.Diagnostics;
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
            recorder = new DecisionServiceRecorder<TContext>(
                config.BatchConfig, 
                config.ContextJsonSerializer,
                config.AuthorizationToken);

            policy = new DecisionServicePolicy<TContext>(UpdatePolicy, 
                string.Format(ModelAddress, config.AuthorizationToken, config.UseLatestPolicy), 
                config.PolicyModelOutputDir);

            mwt = new MwtExplorer<TContext>(config.AppId, recorder);
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
            policy.StopPolling();
            recorder.Flush();
        }

        public void Dispose() { }

        private void UpdatePolicy()
        {
            if (explorer is IConsumePolicy<TContext>)
            {
                ((IConsumePolicy<TContext>)explorer).UpdatePolicy(policy);
                Trace.TraceInformation("Model update succeeded.");
            }
            else
            {
                throw new NotSupportedException("This type of explorer does not currently support updating policy functions.");
            }
        }

        public IRecorder<TContext> Recorder { get { return recorder; } }
        public IPolicy<TContext> Policy { get { return policy; } }
            
        private IExplorer<TContext> explorer;
        private DecisionServiceRecorder<TContext> recorder;
        private DecisionServicePolicy<TContext> policy;
        private MwtExplorer<TContext> mwt;

        #region Constants

        private readonly string ModelAddress = "http://mwtds.azurewebsites.net/Application/GetSelectedModel?token={0}&latest={1}";

        #endregion
    }
}
