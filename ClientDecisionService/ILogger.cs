namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    using MultiWorldTesting.ExploreLibrary;

    public interface ILogger 
    {
        void ReportReward(UniqueEventID uniqueKey, float reward);
        
        void ReportOutcome(UniqueEventID uniqueKey, object outcome);
    }
}
