using System;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public class ModelTransferData
    {
        public DateTimeOffset LastModified { get; set; }

        public string Name { get; set; }

        public string ContentAsBase64 { get; set; }
    }
}
