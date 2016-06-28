namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    using MultiWorldTesting.ExploreLibrary;

    public interface ILogger 
    {
        void ReportReward(string uniqueKey, float reward);

        void ReportOutcome(string uniqueKey, object outcome);
    }
}
