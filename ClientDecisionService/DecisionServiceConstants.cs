using System;

namespace ClientDecisionService
{
    internal static class DecisionServiceConstants
    {
        // TODO: Make test cases set this flag automatically
        //internal static readonly string RedirectionBlobLocation = "http://127.0.0.1:10000/devstoreaccount1/app-locations/{0}";

        internal static readonly string RedirectionBlobLocation = "http://decisionservicestorage.blob.core.windows.net/app-locations/{0}";

        internal static readonly int RetryCount = 3;
        internal static readonly TimeSpan RetryMinBackoff = TimeSpan.FromMilliseconds(500);
        internal static readonly TimeSpan RetryMaxBackoff = TimeSpan.FromMilliseconds(4000);
        internal static readonly TimeSpan RetryDeltaBackoff = TimeSpan.FromMilliseconds(1000);

        internal static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(5);
        internal static readonly TimeSpan PollCancelWait = TimeSpan.FromSeconds(2);
    }
}
