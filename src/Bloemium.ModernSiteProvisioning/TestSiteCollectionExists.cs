using Bloemium.ModernSiteProvisioning.Common;
using Bloemium.ModernSiteProvisioning.Common.Security;
using Bloemium.ModernSiteProvisioning.Common.SharePoint;
using Bloemium.ModernSiteProvisioning.Model;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Bloemium.ModernSiteProvisioning
{
    public static class TestSiteCollectionExists
    {
        private const string FunctionName = "test-site-collection-exists";

        [FunctionName(FunctionName)]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]HttpRequestMessage req,
            TraceWriter log)
        {
            log.Info($"C# HTTP trigger function '{FunctionName}' processed a request.");

            // parse query parameter
            string relativeUrl = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "relativeUrl", true) == 0)
                .Value;

            if (string.IsNullOrEmpty(relativeUrl) == true)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a relative url on the query string.");
            }

            var tenantUrl = new Uri(CloudConfigurationManager.GetSetting("TenantUrl"));
            Uri.TryCreate(tenantUrl, relativeUrl, out Uri fullSiteUrl);
            var siteCollectionExistsMessage = new SiteCollectionExistsMessage() { Exists = false };
            try
            {
                var clientContextManager = new ClientContextManager(new BaseConfiguration(), new CertificateManager());
                using (var ctx = clientContextManager.GetAzureADAppOnlyAuthenticatedContext(fullSiteUrl.AbsoluteUri))
                {
                    ctx.Load(ctx.Web);
                    ctx.ExecuteQuery();
                    siteCollectionExistsMessage.Title = ctx.Web.Title;
                    var type = fullSiteUrl.PathAndQuery.Substring("/sites/".Length, 4).ToUpperInvariant();
                    if (string.IsNullOrEmpty(type) == true)
                    {
                        type = fullSiteUrl.PathAndQuery.Substring("/teams/".Length, 4).ToUpperInvariant();
                    }
                    siteCollectionExistsMessage.AbsoluteUri = fullSiteUrl.AbsoluteUri;
                    siteCollectionExistsMessage.RelativeUrl = fullSiteUrl.PathAndQuery;
                    siteCollectionExistsMessage.Type = type;
                    siteCollectionExistsMessage.Exists = true;
                }
            }
            catch
            {
                // site does not exist
            }
            return req.CreateResponse(HttpStatusCode.OK, siteCollectionExistsMessage);
        }
    }
}
