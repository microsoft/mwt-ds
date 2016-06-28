using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public static class DecisionServiceConstants
    {
        // TODO: needed to run eventuploader stand alone...
        public static readonly string RedirectionBlobLocation = "http://decisionservicestorage.blob.core.windows.net/app-locations/{0}";
        internal static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(5);
    }
}
