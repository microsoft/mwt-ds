using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// When a model cannot be found.
    /// </summary>
    public class ModelNotFoundException : Exception
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public ModelNotFoundException(string message) : base(message) { }
    }
}
