namespace ClientDecisionService
{
    using MultiWorldTesting;

    public interface ILogger<TContext> : MultiWorldTesting.SingleAction.IRecorder<TContext>, MultiWorldTesting.MultiAction.IRecorder<TContext>
    {
        void ReportReward(UniqueEventID uniqueKey, float reward);
        void ReportOutcome(UniqueEventID uniqueKey, object outcome);
        void Flush();
    }
}
