using System.Net.Http;
using System.Threading.Tasks;

namespace JoinServerUploader
{
    /// <summary>
    /// Implementation of <see cref="IHttpResponse"/> that wraps the <see cref="HttpResponseMessage"/> object.
    /// </summary>
    internal class UploaderHttpResponse : IHttpResponse
    {
        internal UploaderHttpResponse(HttpResponseMessage response)
        {
            this.response = response;
        }

        public bool IsSuccessStatusCode
        {
            get
            {
                return response.IsSuccessStatusCode;
            }
        }

        public Task<string> GetContentAsync()
        {
            return this.response.Content.ReadAsStringAsync();
        }

        private HttpResponseMessage response;
    }
}
