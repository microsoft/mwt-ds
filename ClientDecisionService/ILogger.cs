namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    using MultiWorldTesting.ExploreLibrary;

    public interface ILogger<TContext> : MultiWorldTesting.ExploreLibrary.SingleAction.IRecorder<TContext>, MultiWorldTesting.ExploreLibrary.MultiAction.IRecorder<TContext>
    {
        void ReportReward(UniqueEventID uniqueKey, float reward);
        void ReportOutcome(UniqueEventID uniqueKey, object outcome);
        void Flush();
    }
}
