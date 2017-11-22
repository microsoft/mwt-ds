using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.MultiWorldTesting.ClientLibrary
{
    /// <summary>
    /// Defines the policy state for a VowpalWabbit model.
    /// </summary>
    [JsonObject(Id = "stvw")]
    public class VWState
    {
        /// <summary>
        /// The model id used at scoring time.
        /// </summary>
        [JsonProperty("m")]
        public string ModelId { get; set; }
    }
}
