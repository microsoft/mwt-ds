//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Microsoft.DecisionService.Crawl.Data
{
    public class UrlHolder
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }
}
