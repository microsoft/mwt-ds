//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Research.MultiWorldTesting.Contract
{
    /// <summary>
    /// Service constants used for old join service.
    /// </summary>
    public class ServiceConstants
    {
        /// <summary>
        /// public address of join server.
        /// </summary>
        public const string JoinAddress = "http://decisionservice.cloudapp.net";

        /// <summary>
        /// Path for joining.
        /// </summary>
        public const string JoinPostAddress = "/join";

        /// <summary>
        /// Authentication header.
        /// </summary>
        public const string TokenAuthenticationScheme = "Bearer";

        /// <summary>
        /// Authentication scheme.
        /// </summary>
        public const string ConnectionStringAuthenticationScheme = "AzureStorage";

        /// <summary>
        /// Azure container name.
        /// </summary>
        public const string IncompleteContainerPrefix = "incomplete";

        /// <summary>
        /// Azure container name.
        /// </summary>
        public const string JoinedBlobContainerPrefix = "joined-examples";
    }
}
