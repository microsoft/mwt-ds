using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionService
{
    internal static class DecisionServiceConstants
    {
        internal static readonly string CommandCenterAddress = "https://mwtds.azurewebsites.net";
        internal static readonly string ModelAddress = "/Application/GetSelectedModel?token={0}&latest={1}";
        internal static readonly string MetadataAddress = "/Application/GetMetadata?token={0}";

        internal static readonly string ServiceAddress = "http://decisionservice.cloudapp.net";
        internal static readonly string ServicePostAddress = "/join";
        internal static readonly TimeSpan ConnectionTimeOut = TimeSpan.FromMinutes(5);
        internal static readonly string AuthenticationScheme = "Bearer";

        internal static readonly string SettingsContainerName = "application-{0}";
        internal static readonly string LatestSettingsBlobName = "settings";

        internal static readonly int RetryCount = 3;
        internal static readonly TimeSpan RetryMinBackoff = TimeSpan.FromMilliseconds(500);
        internal static readonly TimeSpan RetryMaxBackoff = TimeSpan.FromMilliseconds(4000);
        internal static readonly TimeSpan RetryDeltaBackoff = TimeSpan.FromMilliseconds(1000);

        internal static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(5);
        internal static readonly TimeSpan PollCancelWait = TimeSpan.FromSeconds(2);
    }
}
