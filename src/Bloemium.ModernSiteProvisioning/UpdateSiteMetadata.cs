using Bloemium.ModernSiteProvisioning.Common;
using Bloemium.ModernSiteProvisioning.Common.Security;
using Bloemium.ModernSiteProvisioning.Common.SharePoint;
using Bloemium.ModernSiteProvisioning.Model;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace Bloemium.ModernSiteProvisioning
{
    public static class UpdateSiteMetadata
    {
        private const string FunctionName = "update-site-metadata";

        [FunctionName(FunctionName)]
        public static void Run(
            [ServiceBusTrigger("update-sites-topic", "update-metadata-subscription", AccessRights.Manage, Connection = "ManageTopicConnection")]BrokeredMessage updateMsg,
            TraceWriter log)
        {
            log.Info($"C# ServiceBus trigger function '{FunctionName}' processed message: {updateMsg.MessageId} (Label': {updateMsg.Label}')");

            var somethingWentWrong = false;
            var clientContextManager = new ClientContextManager(new BaseConfiguration(), new CertificateManager());
            var updateMetadataJob = updateMsg.GetBody<UpdateSiteJob>();
            using (var ctx = clientContextManager.GetAzureADAppOnlyAuthenticatedContext(updateMetadataJob.Url))
            {
                // ToDo; currently we only support updating the Title, add more metadata fields to update
                try
                {
                    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("AzureWebJobsStorage"));
                    CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
                    CloudTable SitesTable = tableClient.GetTableReference(CloudConfigurationManager.GetSetting("SitesTable"));

                    // Update columns in Azure Storage table to reflect updates
                    var item = new SitesTableEntry
                    {
                        PartitionKey = updateMetadataJob.Type,
                        RowKey = updateMetadataJob.ID,
                        ETag = "*",
                        Title = updateMetadataJob.Title,
                    };

                    var operation = TableOperation.Merge(item);
                    SitesTable.ExecuteAsync(operation);
                }
                catch (Exception ex)
                {
                    log.Error($"Error occured while updating table entry of {updateMetadataJob.Url}.", ex);
                    somethingWentWrong = true;
                }

                if (somethingWentWrong == false) {
                    ctx.Web.Title = updateMetadataJob.Title;
                    ctx.Web.Update();
                    ctx.ExecuteQuery();
                    log.Info($"Updated title of site collection '{updateMetadataJob.Url}' to '{updateMetadataJob.Title}'");
                }
            }
        }
    }
}