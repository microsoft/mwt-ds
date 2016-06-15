using Microsoft.Azure;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Research.DecisionService.Test
{
    partial class AuthUtility
    {

        internal static async Task<TokenCloudCredentials> GetAuthToken(string uriPrefix,
            ClientCredential cc,
            string tenantID = "72f988bf-86f1-41af-91ab-2d7cd011db47",
            string subscriptionId = "d65ae8da-b9bf-4839-9659-4f3c6f8727f7"
            )
        {
            var authorityUri = "https://login.windows.net/" + tenantID;
            var context = new AuthenticationContext(authorityUri);
            AuthenticationResult result = await context.AcquireTokenAsync(uriPrefix, cc);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to obtain the JWT token");
            };

            TokenCloudCredentials creds = new TokenCloudCredentials(subscriptionId, result.AccessToken);

            return creds;
        }

        internal static SubscriptionCloudCredentials GetCert(
            string subID,
            string base64EncodedCert)
        {
            return new CertificateCloudCredentials(subID,
                new X509Certificate2(Convert.FromBase64String(base64EncodedCert)));
        }

        internal static async Task<TokenCloudCredentials> GetTokenCred()
        {
            // these should be passed in, tho one can be inferred from the other
            string clientId = "9a32a479-6249-42bc-8914-cd5b8119189a";
            string passwd = "ds_pw_1!";
            string subscriptionId = "d65ae8da-b9bf-4839-9659-4f3c6f8727f7";

            // These endpoints are used during authentication and authorization with AAD.
            string AuthorityUri = "https://login.microsoftonline.com/common"; // Azure Active Directory "common" endpoint
            string ResourceUri = "https://management.core.windows.net/";     // Azure service management resource

            // The URI to which Azure AD will redirect in response to an OAuth 2.0 request. This value is
            // specified by you when you register an application with AAD (see ClientId comment). It does not
            // need to be a real endpoint, but must be a valid URI (e.g. https://accountmgmtsampleapp).
            // string RedirectUri = "https://accountmgmtsampleapp";

            // Specify the unique identifier (the "Client ID") for your application. This is required so that your
            // native client application (i.e. this sample) can access the Microsoft Azure AD Graph API. For information
            // about registering an application in Azure Active Directory, please see "Adding an Application" here:
            // https://azure.microsoft.com/documentation/articles/active-directory-integrating-applications/

            // Obtain an access token using the "common" AAD resource. This allows the application
            // to query AAD for information that lies outside the application's tenant (such as for
            // querying subscription information in your Azure account).
            AuthenticationContext authContext = new AuthenticationContext(AuthorityUri);


            ClientCredential cc = new ClientCredential(clientId, passwd);
            AuthenticationResult authResult = await authContext.AcquireTokenAsync(ResourceUri, cc);

            // The first credential object is used when querying for subscriptions, and is therefore
            // not associated with a specific subscription.
            TokenCloudCredentials subscriptionCreds = new TokenCloudCredentials(authResult.AccessToken);

            // These credentials are associated with a subscription, and can therefore be used when
            // creating Resource and Batch management clients for use in manipulating entities within
            // the subscription (e.g. resource groups and Batch accounts).
            TokenCloudCredentials creds = new TokenCloudCredentials(subscriptionId, authResult.AccessToken);

            return creds;
        }
    }
}
