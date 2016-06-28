namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Collection of different Join Server implementation types.
    /// </summary>
    public enum JoinServerType
    {
        /// <summary>
        /// Azure Stream Analytics implementation.
        /// </summary>
        AzureStreamAnalytics = 0,

        /// <summary>
        /// Custom Azure implementation.
        /// </summary>
        CustomSolution
    }
}
