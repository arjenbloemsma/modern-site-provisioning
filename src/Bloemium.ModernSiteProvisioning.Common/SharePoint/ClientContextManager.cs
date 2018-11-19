using Bloemium.ModernSiteProvisioning.Common.Security;
using Microsoft.Azure;
using Microsoft.SharePoint.Client;
using OfficeDevPnP.Core;
using OfficeDevPnP.Core.Utilities;
using System;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace Bloemium.ModernSiteProvisioning.Common.SharePoint
{
    public class ClientContextManager
    {
        private readonly IConfiguration configuration;
        private CertificateManager certificateManager;

        private static readonly string ApplicationId = "ApplicationId";
        private static readonly string TenantId = "TenantId";

        private static readonly string CertificateThumbprint = "CertificateThumbprint";
        private static readonly string CertificateStoreName = "CertificateStoreName";
        private static readonly string CertificateStoreLocation = "CertificateStoreLocation";

        private static readonly string ClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";

        public ClientContextManager(IConfiguration configuration, CertificateManager certificateManager)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.certificateManager = certificateManager ?? throw new ArgumentNullException(nameof(certificateManager));
        }

        public ClientContext Get(string url)
        {
            var ctx = new ClientContext(url);
            ctx.Credentials = CredentialManager.GetSharePointOnlineCredential(CloudConfigurationManager.GetSetting("TenantUrl"));

            return ctx;
        }

        public ClientContext GetAzureADAppOnlyAuthenticatedContext(string siteUrl, string applicationId, string tenantId, string certificateStoreName,
            string certificateStoreLocation, string certificateThumbprint)
        {
            var certificate = this.GetCertificate(certificateStoreName, certificateStoreLocation, certificateThumbprint);

            tenantId = this.GetTenantId(tenantId);

            var authenticationManager = new AuthenticationManager();
            var clientContext = authenticationManager.GetAzureADAppOnlyAuthenticatedContext(siteUrl, applicationId, tenantId, certificate);

            return clientContext;
        }

        public ClientContext GetAzureADAppOnlyAuthenticatedContext(string siteUrl)
        {
            var applicationId = this.configuration.GetSetting(ApplicationId);
            var tenantId = this.configuration.GetSetting(TenantId);
            var certificateStoreName = this.configuration.GetSetting(CertificateStoreName);
            var certificateStoreLocation = this.configuration.GetSetting(CertificateStoreLocation);
            var certificateThumbprint = this.configuration.GetSetting(CertificateThumbprint);

            return this.GetAzureADAppOnlyAuthenticatedContext(siteUrl, applicationId, tenantId, certificateStoreName, certificateStoreLocation,
                certificateThumbprint);
        }

        private string GetTenantId(string tenantId)
        {
            return ClaimsPrincipal.Current.HasClaim(c => c.Type == ClaimType) ? ClaimsPrincipal.Current.FindFirst(ClaimType).Value : tenantId;
        }

        private X509Certificate2 GetCertificate(string storeName, string storeLocation, string thumbprint)
        {
            return this.certificateManager.GetCertificateByThumbprint(storeName, storeLocation, thumbprint);
        }
    }
}
