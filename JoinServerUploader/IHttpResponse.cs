using System.Threading.Tasks;

namespace JoinServerUploader
{
    /// <summary>
    /// Defines methods for custom HTTP response types.
    /// </summary>
    public interface IHttpResponse
    {
        /// <summary>
        /// Gets a value that indicates if the HTTP response was successful.
        /// </summary>
        bool IsSuccessStatusCode { get; }

        /// <summary>
        /// Write the HTTP content to a string as an asynchronous operation.
        /// </summary>
        /// <returns>Returns a task representing the asynchronous operation.</returns>
        Task<string> GetContentAsync();
    }
}
