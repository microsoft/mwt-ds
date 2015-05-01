using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JoinServerUploader
{
    public class EventUploader : IDisposable
    {
        public EventUploader(string authorizationToken) : this(authorizationToken, null) { }

        public EventUploader(string authorizationToken, string loggingServiceBaseAddress)
        {
            this.loggingServiceBaseAddress = loggingServiceBaseAddress ?? Constants.ServiceAddress;

            this.httpClient = new HttpClient();
            this.httpClient.BaseAddress = new Uri(this.loggingServiceBaseAddress);
            this.httpClient.Timeout = Constants.ConnectionTimeOut;
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(Constants.AuthenticationScheme, authorizationToken);
        }

        public async Task UploadAsync(List<IEvent> e, CancellationToken cancelToken)
        {
            List<string> jsonFragments = 
                e.Select(ev => 
                    JsonConvert.SerializeObject(new ExperimentalUnitFragment 
                    { 
                        Id = ev.ID, 
                        Value = ev }))
                .ToList();

            var batch = new EventBatch
            {
                ID = Guid.NewGuid(),
                JsonEvents = jsonFragments
            };

            byte[] jsonByteArray = Encoding.UTF8.GetBytes(EventUploader.BuildJsonMessage(batch));

            using (var jsonMemStream = new MemoryStream(jsonByteArray))
            {
                HttpResponseMessage response = null;

                var retryStrategy = new ExponentialBackoff(Constants.RetryCount,
                Constants.RetryMinBackoff, Constants.RetryMaxBackoff, Constants.RetryDeltaBackoff);

                RetryPolicy retryPolicy = new RetryPolicy<DecisionServiceTransientErrorDetectionStrategy>(retryStrategy);

                response = await retryPolicy.ExecuteAsync(async () =>
                {
                    HttpResponseMessage currentResponse = null;
                    try
                    {
                        currentResponse = await httpClient
                            .PostAsync(Constants.ServicePostAddress, new StreamContent(jsonMemStream), cancelToken)
                            .ConfigureAwait(false);
                    }
                    catch (TaskCanceledException ex) // HttpClient throws this on timeout
                    {
                        // Convert to a different exception otherwise ExecuteAsync will see cancellation
                        throw new HttpRequestException("Request timed out", ex);
                    }
                    return currentResponse.EnsureSuccessStatusCode();
                });

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Unable to upload batch: " + await response.Content.ReadAsStringAsync());
                }
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.httpClient != null)
                {
                    this.httpClient.Dispose();
                    this.httpClient = null;
                }
            }
        }

        private static string BuildJsonMessage(EventBatch batch)
        {
            StringBuilder jsonBuilder = new StringBuilder();

            jsonBuilder.Append("{\"i\":\"" + batch.ID.ToString() + "\",");

            jsonBuilder.Append("\"j\":[");
            jsonBuilder.Append(String.Join(",", batch.JsonEvents));
            jsonBuilder.Append("]}");

            return jsonBuilder.ToString();
        }

        private string loggingServiceBaseAddress;
        private HttpClient httpClient;
    }
}
