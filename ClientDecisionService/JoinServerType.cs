namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Collection of different Join Server implementation types.
    /// </summary>
    public enum JoinServerType
    {
        /// <summary>
        /// Custom Azure implementation.
        /// </summary>
        CustomAzureSolution = 0,

        /// <summary>
        /// Azure Stream Analytics implementation.
        /// </summary>
        AzureStreamAnalytics
    }
}
