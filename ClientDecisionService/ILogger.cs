using MultiWorldTesting.SingleAction;

namespace ClientDecisionService
{
    public interface ILogger<TContext> : IRecorder<TContext>
    {
        void ReportReward(float reward, string uniqueKey);
        void ReportOutcome(string outcomeJson, string uniqueKey);
        void Flush();
    }
}
