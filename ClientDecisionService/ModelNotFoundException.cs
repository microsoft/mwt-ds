using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class ModelNotFoundException : Exception
    {
        public ModelNotFoundException(string message) : base(message) { }
    }
}
