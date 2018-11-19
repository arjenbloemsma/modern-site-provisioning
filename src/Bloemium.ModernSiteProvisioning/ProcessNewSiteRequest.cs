using Bloemium.ModernSiteProvisioning.Common;
using Bloemium.ModernSiteProvisioning.Common.Security;
using Bloemium.ModernSiteProvisioning.Common.SharePoint;
using Bloemium.ModernSiteProvisioning.Model;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Microsoft.SharePoint.Client;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Bloemium.ModernSiteProvisioning
{
    public static class ProcessNewSiteRequest
    {
        private const string FunctionName = "process-new-site-request";

        [FunctionName(FunctionName)]
        public static void Run(
            [ServiceBusTrigger("site-operations-topic", "new-site-requests-subscription", AccessRights.Manage, Connection = "ManageTopicConnection")]BrokeredMessage newSiteMsg,
            [Blob("provisioning-job-files", Connection = "AzureWebJobsStorage")]CloudBlobDirectory blobDirectory,
            [ServiceBus("new-sites-topic", Connection = "ManageTopicConnection")]ICollector<BrokeredMessage> newSitesTopic,
            TraceWriter log)
        {
            log.Info($"C# ServiceBus trigger function '{FunctionName}' processed message: {newSiteMsg.MessageId} (Label': {newSiteMsg.Label}')");

            /*
             * The following line should work, but doesn't, so small workaround here...
             */
            //var createSiteCollectionJob = newSiteMsg.GetBody<CreateSiteCollectionJob>();
            var stream = newSiteMsg.GetBody<Stream>();
            StreamReader streamReader = new StreamReader(stream);
            string createSiteCollectionJobAsJson = streamReader.ReadToEnd();
            var createSiteCollectionJob = JsonConvert.DeserializeObject<CreateSiteCollectionJob>(createSiteCollectionJobAsJson);

            var clientContextManager = new ClientContextManager(new BaseConfiguration(), new CertificateManager());
            var provisioningSiteUrl = CloudConfigurationManager.GetSetting("ProvisioningSite");
            using (var ctx = clientContextManager.GetAzureADAppOnlyAuthenticatedContext(provisioningSiteUrl))
            {
                // Todo: construct URL in a more resililant way ;-) 
                var relativeSiteUrl = new Uri(provisioningSiteUrl).PathAndQuery;
                string serverRelativeFileUrl = $"{relativeSiteUrl}/{createSiteCollectionJob.FolderPath}{createSiteCollectionJob.FileNameWithExtension}";
                var jobFile = ctx.Web.GetFileByServerRelativeUrl(serverRelativeFileUrl);
                ctx.Load(jobFile, jf => jf.ServerRelativeUrl);
                // ToDo: what if file does not exist? 
                var jobFileStream = jobFile.OpenBinaryStream();
                ctx.ExecuteQueryRetry();
                MemoryStream mem = new MemoryStream();
                jobFileStream.Value.CopyTo(mem);
                mem.Position = 0;
                StreamReader reader = new StreamReader(mem, Encoding.Unicode);
                var jobFileAsString = reader.ReadToEnd();
                
                // Store job file in blob storage
                CloudBlockBlob blob = blobDirectory.GetBlockBlobReference(createSiteCollectionJob.FileNameWithExtension);
                blob.Properties.ContentType = "application/json";
                //blob.Metadata.Add("abcd", "12345");
                blob.UploadText(jobFileAsString);
                log.Info($"JobFile '{createSiteCollectionJob.FileNameWithExtension}' stored in Azure blob storage");

                var createSiteCollectionMsg = new BrokeredMessage(createSiteCollectionJob,
                    new DataContractJsonSerializer(typeof(CreateSiteCollectionJob)))
                {
                    ContentType = "application/json",
                    Label = "CreateSiteCollection"
                };
                newSitesTopic.Add(createSiteCollectionMsg);

                // Todo: get list title from configuration.
                // Assume that the web has a list named "PnPProvisioningJobs". 
                List provisioningJobsList = ctx.Web.Lists.GetByTitle("PnPProvisioningJobs");
                ListItem listItem = provisioningJobsList.GetItemById(createSiteCollectionJob.ListItemID);
                listItem["PnPProvisioningJobStatus"] = "Running (creating site collection)"; //ToDo: Should we have different statusses for actual site creation and the applying of the template?
                listItem.Update();

                ctx.ExecuteQuery();
            }
        }
    }
}
