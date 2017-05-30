using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Microsoft.DecisionService.Crawl
{
    /// <summary>
    /// see https://azure.microsoft.com/en-us/documentation/articles/key-vault-use-from-web-application/.
    /// </summary>
    public class KeyVaultHelper
    {
        private readonly ClientAssertionCertificate assertionCert;

        public KeyVaultHelper(StoreLocation storeLocation, string clientId, string thumbprint)
        {
            var clientAssertionCertPfx = CertificateUtil.FindCertificateByThumbprint(storeLocation, thumbprint);
            this.assertionCert = new ClientAssertionCertificate(clientId, clientAssertionCertPfx);
        }

        public async Task<string> GetAccessToken(string authority, string resource, string scope)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            return (await context.AcquireTokenAsync(resource, assertionCert)).AccessToken;
        }
    }
}
