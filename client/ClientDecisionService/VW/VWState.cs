using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    [JsonObject(Id = "stvw")]
    public class VWState
    {
        [JsonProperty("m")]
        public string ModelId { get; set; }
    }
}
