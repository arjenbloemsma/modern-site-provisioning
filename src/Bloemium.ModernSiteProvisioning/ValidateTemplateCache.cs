using Bloemium.ModernSiteProvisioning.Common;
using Bloemium.ModernSiteProvisioning.Common.Security;
using Bloemium.ModernSiteProvisioning.Common.SharePoint;
using Bloemium.ModernSiteProvisioning.Model;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Bloemium.ModernSiteProvisioning
{
    public static class ValidateTemplateCache
    {
        private const string FunctionName = "validate-template-cache";

        [FunctionName(FunctionName)]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequestMessage req,
            TraceWriter log)
        {
            log.Info($"C# HTTP trigger function '{FunctionName}' started.");

            // if update parameter was provided always grab template from SPO
            bool update = req.GetQueryNameValuePairs()
                .Any(q => string.Compare(q.Key, "update", true) == 0);

            var validateTemplateCacheMessage = req.Content.ReadAsAsync<ValidateTemplateCacheMessage>().Result;

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("AzureWebJobsStorage"));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(CloudConfigurationManager.GetSetting("TemplateFilesContainer"));
            var blob = container.GetBlockBlobReference(Path.GetFileName(validateTemplateCacheMessage.TemplateUrl));

            if (update == true || blob.Exists() == false)
            {
                GetTemplateFromSharePointOnline(validateTemplateCacheMessage, blob);
                return req.CreateResponse(HttpStatusCode.OK, $"Template {validateTemplateCacheMessage.TemplateUrl} updated because 'update' parameter was provided or template did not yest exist in blob storage.");
            }
            // Check if we have the template that we need in the blob storage, retrieve it from SPO
            // if we don't have it in blob stoarge or if that version in blob storage is older than
            // one hour
            
            if (blob.Exists() == true)
            {
                blob.FetchAttributes();
                var templateBlobLastModified = blob.Properties.LastModified.Value;
                if (DateTimeOffset.UtcNow.Subtract(templateBlobLastModified) > TimeSpan.FromHours(1.0))
                {
                    // Blob file is older that one hour retrive from SPO
                    GetTemplateFromSharePointOnline(validateTemplateCacheMessage, blob);
                    return req.CreateResponse(HttpStatusCode.OK, $"Template {validateTemplateCacheMessage.TemplateUrl} updated because template in blob storage was too old.");
                }
            }

            return req.CreateResponse(HttpStatusCode.OK, $"Template {validateTemplateCacheMessage.TemplateUrl} not updated because template in blob storage was still valid.");
        }

        private static void GetTemplateFromSharePointOnline(ValidateTemplateCacheMessage validateTemplateCacheMessage, CloudBlockBlob blob)
        {
            var clientContextManager = new ClientContextManager(new BaseConfiguration(), new CertificateManager());
            var provisioningSiteUrl = CloudConfigurationManager.GetSetting("ProvisioningSite");
            using (var ctx = clientContextManager.GetAzureADAppOnlyAuthenticatedContext(provisioningSiteUrl))
            {
                var templateListItem = ctx.Web.GetListItem(validateTemplateCacheMessage.TemplateUrl);
                var file = templateListItem.File;

                // File in SPO is newer, so grab that
                var binaryStream = file.OpenBinaryStream();
                ctx.Load(file);
                ctx.ExecuteQuery();
                if (binaryStream != null && binaryStream.Value != null)
                {
                    // Save to blob
                    binaryStream.Value.Position = 0;
                    blob.UploadFromStream(binaryStream.Value);
                }
            }
        }
    }
}