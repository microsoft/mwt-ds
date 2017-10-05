using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.DecisionService.Crawl
{
    public static class CertificateUtil
    {
        public static X509Certificate2 FindCertificateByThumbprint(StoreLocation storeLocation, string thumbprint)
        {
            X509Store store = new X509Store(StoreName.My, storeLocation);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection col = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    thumbprint, 
                    validOnly:false); // Don't validate certs as they're self-signed
                if (col == null || col.Count == 0)
                {
                    var availableCertThumbprints = string.Join(",", store.Certificates.OfType<X509Certificate2>().Select(c => c.Thumbprint));
                    throw new Exception($"Cannot find certificate in My\\{storeLocation} with thumbprint '{thumbprint}'. Available certs are {availableCertThumbprints}");
                }
                return col[0];
            }
            finally
            {
                store.Close();
            }
        }
    }
}
