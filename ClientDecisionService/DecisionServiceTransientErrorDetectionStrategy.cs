using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using System;

namespace ClientDecisionService
{
    class DecisionServiceTransientErrorDetectionStrategy : ITransientErrorDetectionStrategy
    {
        public bool IsTransient(Exception ex)
        {
            // TODO: examine error codes to determine transient errors
            return true;
        }
    }
}
