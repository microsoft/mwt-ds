using Microsoft.VisualStudio.TestTools.WebTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebLoadTesting
{
    public class ProbabilisticConditional : ConditionalRule
    {
        public float SuccessProbability { get; set; }

        public override void CheckCondition(object sender, ConditionalEventArgs e)
        {
            e.IsMet = new Random().NextDouble() < SuccessProbability;
        }
    }
}
