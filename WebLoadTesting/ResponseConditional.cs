using Microsoft.VisualStudio.TestTools.WebTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebLoadTesting
{
    public class ResponseConditional : ConditionalRule
    {
        public float InconsistencyProbability { get; set; }

        public int ExpectedAction { get; set; }

        public override void CheckCondition(object sender, ConditionalEventArgs e)
        {
            if (e.WebTest.LastRequestOutcome != Outcome.Pass)
            {
                e.IsMet = false;
                return;
            }
            var action = (int)e.WebTest.Context["Action"];
            var reportReward = action == this.ExpectedAction;

            if (new Random().NextDouble() < this.InconsistencyProbability)
                reportReward = !reportReward;

            e.IsMet = reportReward;
        }
    }
}
