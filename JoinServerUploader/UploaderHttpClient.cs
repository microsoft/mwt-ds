using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace JoinServerUploader
{
    /// <summary>
    /// Implementation of <see cref="IHttpClient"/> that wraps the <see cref="HttpClient"/> object.
    /// </summary>
    internal class UploaderHttpClient : IHttpClient
    {
        public void Initialize(string baseAddress, TimeSpan timeout, string authenticationScheme, string authenticationValue)
        {
            if (this.httpClient != null)
            {
                throw new InvalidOperationException("Uploader can only be initialized once.");
            }
            this.httpClient = new HttpClient();
            this.httpClient.BaseAddress = new Uri(baseAddress);
            this.httpClient.Timeout = timeout;
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(authenticationScheme, authenticationValue);
        }

        public async Task<IHttpResponse> PostAsync(string postAddress, string json)
        {
            byte[] jsonByteArray = Encoding.UTF8.GetBytes(json);

            using (var jsonMemStream = new MemoryStream(jsonByteArray))
            {
                HttpResponseMessage responseTask = await this.httpClient.PostAsync(postAddress, new StreamContent(jsonMemStream)).ConfigureAwait(false);

                return new UploaderHttpResponse(responseTask);
            }
        }

        /// <summary>
        /// Disposes the current object and all internal members.
        /// </summary>
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

        private HttpClient httpClient;
    }
}
