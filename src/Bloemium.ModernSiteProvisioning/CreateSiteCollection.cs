using Bloemium.ModernSiteProvisioning.Common;
using Bloemium.ModernSiteProvisioning.Common.Security;
using Bloemium.ModernSiteProvisioning.Common.SharePoint;
using Bloemium.ModernSiteProvisioning.Model;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Online.SharePoint.TenantAdministration;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Bloemium.ModernSiteProvisioning
{
    public static class CreateSiteCollection
    {
        private const string FunctionName = "create-site-collection";

        [FunctionName(FunctionName)]
        public static async void Run(
            [ServiceBusTrigger("new-sites-topic", "create-site-subscription", AccessRights.Manage, Connection = "ManageTopicConnection")]BrokeredMessage newSiteMsg,
            [OrchestrationClient]DurableOrchestrationClient orchestrationClient,
            TraceWriter log)
        {
            log.Info($"C# Service Bus trigger function '{FunctionName}' processed message: {newSiteMsg.MessageId}");

            /*
             * The following line should work, but doesn't, so small workaround here...
             */
            //var createSiteCollectionJob = newSiteMsg.GetBody<CreateSiteCollectionJob>();
            var stream = newSiteMsg.GetBody<Stream>();
            StreamReader streamReader = new StreamReader(stream);
            string createSiteCollectionJobAsJson = streamReader.ReadToEnd();
            var createSiteCollectionJob = JsonConvert.DeserializeObject<CreateSiteCollectionJob>(createSiteCollectionJobAsJson);

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("AzureWebJobsStorage"));
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(CloudConfigurationManager.GetSetting("JobFilesContainer"));

            var blob = container.GetBlobReference(createSiteCollectionJob.FileNameWithExtension);
            var blobStream = new MemoryStream();
            blob.DownloadToStream(blobStream);
            streamReader = new StreamReader(blobStream);
            blobStream.Position = 0;
            string blobContent = streamReader.ReadToEnd();

            JObject provisioningJobFile = JObject.Parse(blobContent);
            var provisioningTemplateUrl = provisioningJobFile["ProvisioningTemplateUrl"].Value<string>();
            var tenantUrl = new Uri(CloudConfigurationManager.GetSetting("TenantUrl"));
            Uri.TryCreate(tenantUrl, 
                provisioningJobFile["RelativeUrl"].Value<string>(), 
                out Uri fullSiteUrl);
            //Properties of the New SiteCollection
            var siteCreationProperties = new SiteCreationProperties
            {
                //New SiteCollection Url
                Url = fullSiteUrl.AbsoluteUri,
                //Title of the Root Site
                Title = provisioningJobFile["SiteTitle"].Value<string>(),
                //Template of the Root Site. Using Team Site for now.
                Template = "STS#0",
                //Owner of thge Site
                Owner = provisioningJobFile["Owner"].Value<string>(),
                //Storage Limit in MB
                StorageMaximumLevel = provisioningJobFile["StorageMaximumLevel"].Value<int>(),
                StorageWarningLevel = provisioningJobFile["StorageWarningLevel"].Value<int>(),
                //UserCode Resource Points Allowed
                UserCodeMaximumLevel = provisioningJobFile["UserCodeMaximumLevel"].Value<int>(),
                UserCodeWarningLevel = provisioningJobFile["UserCodeWarningLevel"].Value<int>(),
                //TimeZone
                TimeZoneId = provisioningJobFile["TimeZone"].Value<int>(),
            };

            var clientContextManager = new ClientContextManager(new BaseConfiguration(), new CertificateManager());
            using (var adminCtx = clientContextManager.GetAzureADAppOnlyAuthenticatedContext(CloudConfigurationManager.GetSetting("TenantAdminUrl")))
            {
                var tenant = new Tenant(adminCtx);
                //Create the SiteCollection
                tenant.CreateSite(siteCreationProperties);

                try
                {
                    adminCtx.Load(tenant);
                    adminCtx.ExecuteQuery();
                    log.Info($"Initiated creation of site collection: {siteCreationProperties.Url}");

                    string instanceId = await orchestrationClient
                        .StartNewAsync("monitor-site-collection-creation", new MonitorSiteCollectionCreationData
                        {
                            FullSiteUrl = siteCreationProperties.Url,
                            ListItemID = createSiteCollectionJob.ListItemID,
                            ProvisioningTemplateUrl = provisioningTemplateUrl,
                            TimeStamp = DateTime.Now,
                            CreateSiteCollectionJob = createSiteCollectionJob
                        });
                    // ToDo: Update value of field provisioning status in Azure Storage Table "Sites"
                    log.Info($"Durable Function Ochestration for site collection creation started: {instanceId}");
                }
                catch (Exception ex)
                {
                    log.Error($"Something went wrong while creating site collection: {siteCreationProperties.Url}.", ex);
                }
            }
        }
    }
}
