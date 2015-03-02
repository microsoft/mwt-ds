using System;

namespace ClientDecisionService
{
    public class ModelTransferData
    {
        public DateTimeOffset LastModified { get; set; }

        public string Name { get; set; }

        public string ContentAsBase64 { get; set; }
    }
}
