//------------------------------------------------------------------------------
// <copyright>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace ClientDecisionServiceTest
{
    using System.Collections.Specialized;
    using System.Text.RegularExpressions;

    public enum AuthorizationType
    {
        DecisionService,
        AzureStorage
    }

    public sealed class Authorization
    {
        private static readonly Regex AuthorizationPattern = new Regex("^(?'type'Bearer|AzureStorage) (?'value'.+)$", RegexOptions.Compiled);

        public Authorization(NameValueCollection headers)
        {
            var authorizationHeader = headers["Authorization"];
            var authorizationMatch = AuthorizationPattern.Match(authorizationHeader);

            switch (authorizationMatch.Groups["type"].Value)
            {
                case "Bearer":
                    this.Token = authorizationMatch.Groups["value"].Value;
                    this.Type = AuthorizationType.DecisionService;
                    break;

                case "AzureStorage":
                    this.AzureStorageConnectionString = authorizationMatch.Groups["value"].Value;
                    this.Type = AuthorizationType.AzureStorage;
                    break;
            }
        }

        public AuthorizationType Type { get; private set; }

        public string Token { get; private set; }

        public string AzureStorageConnectionString { get; private set; }
    }
}
