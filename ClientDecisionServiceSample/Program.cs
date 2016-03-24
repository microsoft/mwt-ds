using Microsoft.Research.MultiWorldTesting.ClientLibrary;
using Microsoft.Research.MultiWorldTesting.ClientLibrary.SingleAction;
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
            // to optimize an action from a set of actions.
            //SingleActionSamples.SampleCodeUsingSimpleContext();

            // Sample code showing how to use ASA join server along with
            // context objects which are json-formatted, where the objective
            // is to choose one single action.
            //SingleActionSamples.SampleCodeUsingASAWithJsonContext();

            // Sample code showing how to tell the client to upload data
            // to the ASA join server.
            MultiActionSamples.SampleCodeUsingASAJoinServer();

            // Sample code showing how to use ASA join server along with
            // context objects which are json-formatted, where the objective
            // is to choose a ranking over actions.
            //MultiActionSamples.SampleCodeUsingASAWithJsonContext();
        }
    }
}
