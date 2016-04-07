using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal static class DecisionServiceConstants
    {
        internal static readonly string RedirectionBlobLocation = "http://decisionservicestorage.blob.core.windows.net/app-locations/{0}";
        internal static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(5);
    }
}
