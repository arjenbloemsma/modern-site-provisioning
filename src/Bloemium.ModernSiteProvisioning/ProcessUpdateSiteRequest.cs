using Bloemium.ModernSiteProvisioning.Model;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Linq;

namespace Bloemium.ModernSiteProvisioning
{
    public static class ProcessUpdateSiteRequest
    {
        private const string FunctionName = "process-update-site-request";

        [FunctionName(FunctionName)]
        public static void Run(
            [ServiceBusTrigger("site-operations-topic", "update-site-requests-subscription", AccessRights.Manage, Connection = "ManageTopicConnection")]BrokeredMessage updateMsg,
            [ServiceBus("update-sites-topic", Connection = "ManageTopicConnection")]ICollector<BrokeredMessage> siteUpdatesTopic,
            TraceWriter log)
        {
            log.Info($"C# Service Bus trigger function '{FunctionName}' processed message: {updateMsg.MessageId} (Label': {updateMsg.Label}')");

            var updateMetadataJob = updateMsg.GetBody<UpdateSiteJob>();
            var relativeSiteUrl = new Uri(updateMetadataJob.Url).PathAndQuery;

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable SitesTable = tableClient.GetTableReference(CloudConfigurationManager.GetSetting("SitesTable"));

            //ToDo: augment message here with info from Azure Storage Table
            //ToDo: check if query actually returned something usefull
            var query = from entity in SitesTable.CreateQuery<SitesTableEntry>()
                        where entity.URL.Equals(relativeSiteUrl)
                        select entity;
            updateMetadataJob.Type = relativeSiteUrl.Substring("/sites/".Length, 4).ToUpperInvariant();
            updateMetadataJob.ID = query.FirstOrDefault().RowKey;
            var augmentedMsg = new BrokeredMessage(updateMetadataJob)
            {
                Label = updateMsg.Label
            };
            siteUpdatesTopic.Add(augmentedMsg);
        }
    }
}
