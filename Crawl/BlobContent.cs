//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.DecisionService.Crawl
{
    public sealed class BlobContent
    {
        public string Value { get; set; }

        public DateTime Expires { get; set; }

        public JObject Output { get; set; }
    }
}
