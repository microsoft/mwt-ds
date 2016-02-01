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
            // Sample code showing how to use the simple interface of the client library
            // to optimize an action from a set of actions.
            //SingleActionSamples.SampleCodeUsingSimpleContext();

            MultiActionSamples.SampleCodeUsingASAJoinServer();
        }
    }
}
