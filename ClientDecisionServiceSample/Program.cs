using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction;
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
            Trace.Listeners.Add(new ConsoleTraceListener());

            // Sample code showing how to use the simple interface of the client library
            // to perform news recommendation.
            Sample1.NewsRecommendation();
        }
    }
}
