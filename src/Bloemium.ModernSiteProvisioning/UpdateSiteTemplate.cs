using Bloemium.ModernSiteProvisioning.Common;
using Bloemium.ModernSiteProvisioning.Common.Security;
using Bloemium.ModernSiteProvisioning.Common.SharePoint;
using Bloemium.ModernSiteProvisioning.Model;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace Bloemium.ModernSiteProvisioning
{
    public static class UpdateSiteTemplate
    {
        private const string FunctionName = "update-site-template";

        [FunctionName(FunctionName)]
        public static void Run(
            [ServiceBusTrigger("update-sites-topic", "update-template-subscription", AccessRights.Manage, Connection = "ManageTopicConnection")]BrokeredMessage updateMsg,
            [ServiceBus("update-sites-topic", Connection = "ManageTopicConnection")]ICollector<BrokeredMessage> updateSitesTopic,
            TraceWriter log)
        {
            log.Info($"C# ServiceBus trigger function '{FunctionName}' processed message: {updateMsg.MessageId} (Label': {updateMsg.Label}')");

            /*
             * The following line should work, but doesn't, so small workaround here...
             */
            //var applyProvisioningTemplateJobAsJson = updateMsg.GetBody<ApplyProvisioningTemplateJob>();
            var stream = updateMsg.GetBody<Stream>();
            StreamReader streamReader = new StreamReader(stream);
            string applyProvisioningTemplateJobAsJson = streamReader.ReadToEnd();
            var applyProvisioningTemplateJob = JsonConvert.DeserializeObject<ApplyProvisioningTemplateJob>(applyProvisioningTemplateJobAsJson);

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("AzureWebJobsStorage"));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(CloudConfigurationManager.GetSetting("TemplateFilesContainer"));
            var blob = container.GetBlockBlobReference(Path.GetFileName(applyProvisioningTemplateJob.ProvisioningTemplateUrl));

            if (blob.Exists() == false)
            {
                GetTemplateFromSharePointOnline(applyProvisioningTemplateJob.ProvisioningTemplateUrl, blob);
                log.Info($"Template {applyProvisioningTemplateJob.ProvisioningTemplateUrl} added since it did not yet exist in blob storage.");
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
                    GetTemplateFromSharePointOnline(applyProvisioningTemplateJob.ProvisioningTemplateUrl, blob);
                    log.Info($"Template {applyProvisioningTemplateJob.ProvisioningTemplateUrl} updated because template in blob storage was too old.");
                }
            }
            applyProvisioningTemplateJob.Checked = true;
            var applyProvisioningTemplateMsg = new BrokeredMessage(applyProvisioningTemplateJob,
                new DataContractJsonSerializer(typeof(ApplyProvisioningTemplateJob)))
            {
                ContentType = "application/json",
                Label = "ApplySiteTemplate"
            };
            updateSitesTopic.Add(applyProvisioningTemplateMsg);
        }

        private static void GetTemplateFromSharePointOnline(string templateUrl, CloudBlockBlob blob)
        {
            var clientContextManager = new ClientContextManager(new BaseConfiguration(), new CertificateManager());
            var provisioningSiteUrl = CloudConfigurationManager.GetSetting("ProvisioningSite");
            using (var ctx = clientContextManager.GetAzureADAppOnlyAuthenticatedContext(provisioningSiteUrl))
            {
                var templateListItem = ctx.Web.GetListItem(templateUrl);
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