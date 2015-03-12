using MultiWorldTesting;
using System;
using System.Diagnostics;
using System.Globalization;
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

            // COMMENT: I'd leave the model address in the string to improve readability
            policy = new DecisionServicePolicy<TContext>(UpdatePolicy, 
                string.Format(CultureInfo.InvariantCulture, ModelAddress, config.AuthorizationToken, config.UseLatestPolicy), 
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
            IConsumePolicy<TContext> consumePolicy = explorer as IConsumePolicy<TContext>;
            if (consumePolicy != null)
            {
                consumePolicy.UpdatePolicy(policy);
                Trace.TraceInformation("Model update succeeded.");
            }
            else
            {
                throw new NotSupportedException("This type of explorer does not currently support updating policy functions.");
            }
        }

        public IRecorder<TContext> Recorder { get { return recorder; } }
        public IPolicy<TContext> Policy { get { return policy; } }

        private readonly IExplorer<TContext> explorer;
        private readonly DecisionServiceRecorder<TContext> recorder;
        private readonly DecisionServicePolicy<TContext> policy;
        private readonly MwtExplorer<TContext> mwt;

        #region Constants

        private const string ModelAddress = "http://mwtds.azurewebsites.net/Application/GetSelectedModel?token={0}&latest={1}";

        #endregion
    }
}
