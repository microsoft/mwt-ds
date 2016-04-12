using Microsoft.Research.MultiWorldTesting.ExploreLibrary;
using Microsoft.Research.MultiWorldTesting.JoinUploader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceSample
{
    public class SampleCodeUsingEventHubUploaderClass
    {
        public static void SampleCodeUsingEventHubUploader()
        {
            var uploader = new EventUploaderASA("", "");

            Stopwatch sw = new Stopwatch();

            int numEvents = 100;
            var events = new IEvent[numEvents];
            for (int i = 0; i < numEvents; i++)
            {
                events[i] = new Interaction
                {
                    Key = i.ToString(),
                    Value = 1,
                    Context = "context " + i,
                    ExplorerState = new GenericExplorerState { Probability = (float)i / numEvents }
                };
            }
            //await uploader.UploadAsync(events[0]);
            uploader.Upload(events[0]);

            sw.Start();

            //await uploader.UploadAsync(events.ToList());
            uploader.Upload(events.ToList());

            events = new IEvent[numEvents];
            for (int i = 0; i < numEvents; i++)
            {
                events[i] = new Observation
                {
                    Key = i.ToString(),
                    Value = "observation " + i
                };
            }
            //await uploader.UploadAsync(events.ToList());
            uploader.Upload(events.ToList());

            Console.WriteLine(sw.Elapsed);
        }


    }
}
