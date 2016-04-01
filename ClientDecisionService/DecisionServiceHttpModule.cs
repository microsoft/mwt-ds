using System.ComponentModel;
using System.Diagnostics;
using System.Web;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    [EditorBrowsable(EditorBrowsableState.Never)] // Hide this from VS code browsing
    class DecisionServiceHttpModule : IHttpModule
    {
        public void Init(HttpApplication context)
        { 
            /* do nothing */
            Trace.TraceInformation("Decision Service Cache detected Init event.");
        }

        public void Dispose()
        {
            DecisionServiceClient.EvictAll();
            Trace.TraceInformation("Decision Service Cache detected Dispose event.");
        }
    }
}
