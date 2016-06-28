using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.Research.MultiWorldTesting.ExploreLibrary.SingleAction;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;

namespace ClientDecisionServiceSample
{
    class Program
    {
        static void Main(string[] args)
        {
            Trace.Listeners.Add(new ConsoleTraceListener());

            // Sample code showing how to use the simple interface of the client library
            // to perform news recommendation. This example assumes the set of topics 
            // is constant for each decision varies.
            Sample.NewsRecommendation();

            // Sample code showing how to use the simple interface of the client library
            // to perform news recommendation. This example assumes each topic has its 
            // own set of features and the set of topics available for each decision varies.
            SampleActionDependentFeature.NewsRecommendation();
        }
    }
}
