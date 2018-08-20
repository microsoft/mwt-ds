//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Crawl
{
    internal static class Constants
    {
        public const int MaxRequestSizeAnsi = 10240;
        public const int MaxRequestSizeUtf16 = MaxRequestSizeAnsi / 2;
    }
}
