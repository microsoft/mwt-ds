using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using System;
using System.Net;

namespace Microsoft.Research.DecisionService.Uploader
{
    class JoinServiceTransientErrorDetectionStrategy : ITransientErrorDetectionStrategy
    {
        public bool IsTransient(Exception ex)
        {
            if (ex is WebException)
            {
                WebException wex = ex as WebException;
                HttpWebResponse response = wex.Response as HttpWebResponse;
                if (response != null && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
