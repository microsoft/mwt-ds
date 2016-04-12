using Microsoft.Research.MultiWorldTesting.JoinUploader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceSample.Policy___non_action_dependent_features
{
    public static class Sample4
    {
        /***** Copy & Paste your authorization token here *****/
        static readonly string MwtServiceToken = "";

        /// <summary>
        /// Sample code for using the standalone <see cref="EventUploader"/> API to upload data to the join server. 
        /// </summary>
        public static void SampleStandaloneUploader()
        {
            if (String.IsNullOrWhiteSpace(MwtServiceToken))
            {
                Console.WriteLine("Please specify a valid authorization token.");
                return;
            }

            var uploader = new EventUploader();

            // TODO: remove this sample code since in-mem join server is no longer supported?
            // Initialize the uploader with a valid authorization token.
            uploader.InitializeWithToken(MwtServiceToken);

            // Specify the callback when a package of data was sent successfully.
            uploader.PackageSent += (sender, pse) => { Console.WriteLine("Uploaded {0} events.", pse.Records.Count()); };

            // Actual uploading of data.
            uploader.Upload(Interaction.CreateEpsilonGreedy(key: "sample-upload", context: "sample context", action: 1, probability: 0.5f));

            // Flush to ensure any remaining data is uploaded.
            uploader.Flush();
        }
    }
}
