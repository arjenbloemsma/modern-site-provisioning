using System;
using System.Security.Cryptography.X509Certificates;

namespace Bloemium.ModernSiteProvisioning.Common.Security
{
    public class CertificateManager
    {
        public X509Certificate2 GetCertificateByThumbprint(string storeName, string locationName, string thumbprint)
        {
            if (storeName == null)
            {
                throw new ArgumentNullException(nameof(storeName));
            }

            if (locationName == null)
            {
                throw new ArgumentNullException(nameof(locationName));
            }

            if (thumbprint == null)
            {
                throw new ArgumentNullException(nameof(thumbprint));
            }

            if (Enum.TryParse(storeName, out StoreName sn) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(storeName));
            }

            if (Enum.TryParse(locationName, out StoreLocation sl) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(locationName));
            }

            using (var certStore = new X509Store(sn, sl))
            {
                certStore.Open(OpenFlags.ReadOnly);

                var certCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (certCollection == null || certCollection.Count == 0)
                {
                    return null;
                }

                return certCollection[0];
            }
        }
    }
}