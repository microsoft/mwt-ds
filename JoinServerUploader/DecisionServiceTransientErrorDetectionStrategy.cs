using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using System;
using System.Net;

namespace JoinServerUploader
{
    class DecisionServiceTransientErrorDetectionStrategy : ITransientErrorDetectionStrategy
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
