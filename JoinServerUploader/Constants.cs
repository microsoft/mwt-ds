using System;

namespace Microsoft.Research.DecisionService.Uploader
{
    internal static class Constants
    {
        internal static readonly TimeSpan ConnectionTimeOut = TimeSpan.FromMinutes(5);

        internal static readonly int RetryCount = 3;
        internal static readonly TimeSpan RetryMinBackoff = TimeSpan.FromMilliseconds(500);
        internal static readonly TimeSpan RetryMaxBackoff = TimeSpan.FromMilliseconds(4000);
        internal static readonly TimeSpan RetryDeltaBackoff = TimeSpan.FromMilliseconds(1000);
    }
}
