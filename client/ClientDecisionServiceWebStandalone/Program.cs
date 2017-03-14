using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientDecisionServiceWebStandalone
{
    class Program
    {
        static int Main(string[] args)
        {
            // Input parameters
            if (args.Length != 1)
            {
                Console.Error.WriteLine($"Usage: {Process.GetCurrentProcess().ProcessName} config.json");
                return -1;
            }

            var config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(args[0]));
            DecisionServiceController.Initialize(config.DecisionServiceUrl);

            TelemetryConfiguration.Active.InstrumentationKey = config.AppInsightsKey;

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseStartup<Startup>()
                .UseUrls(config.Url)
                .Build();

            host.Run();

            return 0;
        }
    }
}
