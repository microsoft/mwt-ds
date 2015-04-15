using MultiWorldTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionService
{
    public interface ILogger<TContext> : IRecorder<TContext>
    {
        void ReportReward(float reward, string uniqueKey);
        void ReportOutcome(string outcomeJson, string uniqueKey);
        void Flush();
    }
}
