using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using System.Diagnostics;
using Microsoft.ApplicationInsights.DataContracts;

namespace ClientDecisionServiceWebStandalone
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            Configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
        }

        public IConfigurationRoot Configuration { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            //loggerFactory.AddConsole();

            var telemetry = new TelemetryClient();
            app.Use(async (req, next) =>
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var startTime = DateTimeOffset.Now;

                    await next.Invoke();

                    var request = new RequestTelemetry(
                       name: req.Request.Path,
                       startTime: startTime,
                       duration: stopwatch.Elapsed,
                       responseCode: req.Response.StatusCode.ToString(),
                       success: req.Response.StatusCode >= 200 && req.Response.StatusCode < 300);

                    telemetry.TrackRequest(request);
                }
                catch (Exception e)
                {
                    telemetry.TrackException(e, new Dictionary<string, string>
                    {
                        { "path", req.Request.Path }
                    });

                    throw e;
                }
            });

            app.UseMvc();
        }
    }
}
