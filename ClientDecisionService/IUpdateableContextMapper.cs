using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    public interface IUpdateableContextMapper
    {
        bool ModelUpdate(Stream model);
    }
}
