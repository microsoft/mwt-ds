//------------------------------------------------------------------------------
// <copyright company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Crawl
{
    public class CognitiveService : HttpCachedService
    {
        private readonly string queryParams;

        public CognitiveService(string containerName, string queryParams = null) : base(containerName)
        {
            this.queryParams = queryParams;
        }

        protected override void Initialize()
        {
            // TODO: need to re-create client (can't just update base address if the key changes...)
            //if (this.client.DefaultRequestHeaders.Contains("Ocp-Apim-Subscription-Key"))
            //    this.client.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");

            this.client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            if (!string.IsNullOrEmpty(queryParams))
                this.client.BaseAddress = new Uri(this.client.BaseAddress.ToString() + queryParams);
        }
    }
}
