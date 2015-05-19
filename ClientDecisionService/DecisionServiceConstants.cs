using System;

namespace ClientDecisionService
{
    internal static class DecisionServiceConstants
    {
        internal static readonly string MwtServiceAzureStorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=decisionservicestorage;AccountKey=2/205EDbKENilswA/Gdtr2tSM/wauWxqLn8/EuXnTU1Ma/3ZFNxBjLZOqUN+DcZ4gjtndnIviE+jzm6jJJ4dgw==";

        internal static readonly int RetryCount = 3;
        internal static readonly TimeSpan RetryMinBackoff = TimeSpan.FromMilliseconds(500);
        internal static readonly TimeSpan RetryMaxBackoff = TimeSpan.FromMilliseconds(4000);
        internal static readonly TimeSpan RetryDeltaBackoff = TimeSpan.FromMilliseconds(1000);

        internal static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(5);
        internal static readonly TimeSpan PollCancelWait = TimeSpan.FromSeconds(2);
    }
}
