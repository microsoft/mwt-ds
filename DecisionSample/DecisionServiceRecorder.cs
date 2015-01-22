using MultiWorldTesting;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DecisionSample
{
    // TODO: rename Recorder to Logger?
    internal class DecisionServiceRecorder<TContext> : IRecorder<TContext>, IDisposable
    {
        public DecisionServiceRecorder(BatchingConfiguration batchConfig, 
            Func<TContext, string> contextSerializer, 
            int experimentalUnitDurationInSeconds,
            string authorizationToken) 
        {
            this.batchConfig = batchConfig;
            this.contextSerializer = contextSerializer;
            this.experimentalUnitDurationInSeconds = experimentalUnitDurationInSeconds;
            this.authorizationToken = authorizationToken;

            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(this.ServiceAddress);
            httpClient.Timeout = TimeSpan.FromSeconds(this.ConnectionTimeOutInSeconds);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(this.AuthenticationScheme, this.authorizationToken);

            this.batch = new Subject<IEvent>();
            this.batch.Window(batchConfig.Duration)
                .Select(w => w.Buffer(batchConfig.EventCount, batchConfig.BufferSize, ev => ev.Measure()))
                .SelectMany(buffer => buffer)
                .Subscribe(events => this.BatchProcess(events));
        }

        public void Record(TContext context, uint action, float probability, string uniqueKey)
        {
            this.batch.OnNext(new Interaction { 
                ID = uniqueKey,
                Action = (int)action,
                Probability = probability,
                Context = this.contextSerializer(context)
            });
        }

        public void ReportReward(float reward, string uniqueKey)
        {
            this.batch.OnNext(new Observation { 
                ID = uniqueKey,
                Value = reward.ToString()
            });
        }

        public void ReportOutcome(string outcomeJson, string uniqueKey)
        {
            this.batch.OnNext(new Observation { 
                ID = uniqueKey,
                Value = outcomeJson
            });
        }

        // TODO: at the time of server communication, if the client is out of memory (or meets some predefined upper bound):
        // 1. It can block the execution flow.
        // 2. Or drop events.
        private async void BatchProcess(IList<IEvent> events)
        {
            using (var jsonMemStream = new MemoryStream())
            using (var jsonWriter = new JsonTextWriter(new StreamWriter(jsonMemStream)))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, new EventBatch 
                {
                    Events = events, 
                    ExperimentalUnitDurationInSeconds = this.experimentalUnitDurationInSeconds 
                });
                    
                jsonWriter.Flush();
                jsonMemStream.Position = 0;

#if TEST
                await this.BatchLog("decision_service_test_output", jsonMemStream);
#else
                await this.BatchUpload(jsonMemStream);
#endif
            }
        }

        private async Task BatchLog(string batchFile, MemoryStream jsonMemStream)
        {
            File.WriteAllText(batchFile, Encoding.UTF8.GetString(jsonMemStream.ToArray()));
        }
        
        private async Task BatchUpload(MemoryStream jsonMemStream)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(this.ServiceAddress);
                client.Timeout = TimeSpan.FromSeconds(this.ConnectionTimeOutInSeconds);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(this.AuthenticationScheme, this.authorizationToken);

                HttpResponseMessage response = await client.PostAsync(this.ServicePostAddress, new StreamContent(jsonMemStream));
                if (!response.IsSuccessStatusCode)
                {
                    Task<string> taskReadResponse = response.Content.ReadAsStringAsync();
                    taskReadResponse.Wait();
                    string responseMessage = taskReadResponse.Result;

                    // TODO: throw exception with custom message?
                }
            }
        }

        // Internally, background tasks can get back latest model version as a return value from the HTTP communication with Ingress worker

        public void Dispose() 
        {
            httpClient.Dispose();
        }

        #region Members
        private BatchingConfiguration batchConfig;
        private Func<TContext, string> contextSerializer;
        private Subject<IEvent> batch;
        private int experimentalUnitDurationInSeconds;
        private string authorizationToken;
        private HttpClient httpClient;
        #endregion

        #region Constants
        //private readonly string ServiceAddress = "http://decisionservice.cloudapp.net";
        private readonly string ServiceAddress = "http://localhost:1362";
        private readonly string ServicePostAddress = "/DecisionService.svc/PostExperimentalUnits";
        private readonly int ConnectionTimeOutInSeconds = 60;
        private readonly string AuthenticationScheme = "Bearer";
        #endregion
    }
}
