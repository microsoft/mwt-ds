using System;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.JoinUploader
{
    /// <summary>
    /// The custom HTTP client type for handling HTTP requests.
    /// </summary>
    public interface IHttpClient : IDisposable
    {
        /// <summary>
        /// Initializes the HTTP client with appropriate settings.
        /// </summary>
        /// <param name="baseAddress">The base address of the HTTP server.</param>
        /// <param name="timeout">The <see cref="TimeSpan"/> object representing the timeout period.</param>
        /// <param name="authenticationScheme">The authentication scheme.</param>
        /// <param name="authenticationValue">The authentication value.</param>
        void Initialize(string baseAddress, TimeSpan timeout, string authenticationScheme, string authenticationValue);

        /// <summary>
        /// Post the 
        /// </summary>
        /// <param name="postAddress">Send a POST request to the specified address as an asynchronous operation.</param>
        /// <param name="json">The JSON value to be included in the request.</param>
        /// <returns>Returns a task representing the asynchronous operation.</returns>
        Task<IHttpResponse> PostAsync(string postAddress, string json);
    }
}
