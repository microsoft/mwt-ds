using ClientDecisionService;
using ClientDecisionService.SingleAction;
using Microsoft.Research.DecisionService.Uploader;
using MultiWorldTesting;
using MultiWorldTesting.SingleAction;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ClientDecisionServiceSample
{
    class Program
    {
        static void Main(string[] args)
        {
            //SingleActionSamples.SampleCodeUsingSimpleContext();
            MultiActionSamples.SampleCodeUsingASAJoinServer();
        }
    }
}
