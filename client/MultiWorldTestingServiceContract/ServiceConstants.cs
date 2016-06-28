//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Research.MultiWorldTesting.Contract
{
    public class ServiceConstants
    {
        // Join Server
        public const string JoinAddress = "http://decisionservice.cloudapp.net";
        public const string JoinPostAddress = "/join";
        public const string TokenAuthenticationScheme = "Bearer";
        public const string ConnectionStringAuthenticationScheme = "AzureStorage";

        public const string IncompleteContainerPrefix = "incomplete";
        public const string JoinedBlobContainerPrefix = "joined-examples";
    }
}
