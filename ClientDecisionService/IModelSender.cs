using System;
using System.IO;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    internal interface IModelSender
    {
        event EventHandler<Stream> Send;
    }
}
