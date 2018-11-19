using Bloemium.ModernSiteProvisioning.Model;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Bloemium.ModernSiteProvisioning
{
    public static class RegisterNewSite
    {
        private const string FunctionName = "register-new-site";

        [FunctionName(FunctionName)]
        [return: Table("Sites")]
        public static SitesTableEntry Run(
            [ServiceBusTrigger("new-sites-topic", "register-site-subscription", AccessRights.Manage, Connection = "ManageTopicConnection")]BrokeredMessage newSiteMsg,
            TraceWriter log)
        {
            log.Info($"C# Service Bus trigger function '{FunctionName}' processed message: {newSiteMsg.MessageId} (Label': {newSiteMsg.Label}')");
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
            var relativeUrl = provisioningJobFile["RelativeUrl"].Value<string>();
            var provisioningTemplateUrl = provisioningJobFile["ProvisioningTemplateUrl"].Value<string>();
            // get JSON result objects into a list
            IList<JToken> parameters = provisioningJobFile["TemplateParameters"].Children().ToList();
            // serialize JSON results into .NET objects
            IDictionary<string, string> templateParameters = new Dictionary<string, string>();
            foreach (JProperty parameter in parameters)
            {
                templateParameters.Add(parameter.Name, parameter.Value.ToObject<string>());
            }

            // ToDo; evealuate if this are the best values for Partitionkey and Rowkey
            return new SitesTableEntry
            {
                PartitionKey = provisioningTemplateUrl,
                RowKey = relativeUrl,
                Title = provisioningJobFile["SiteTitle"].Value<string>(),
                URL = relativeUrl,
                ProvisioningStatus = 0,
                Updated = null,
                ProvisioningTemplateUrl = provisioningTemplateUrl
            };
        }

        public static string TryGetReturnValue(this IDictionary<string, string> dictionary, string key)
        {
            dictionary.TryGetValue(key, out string value);
            return value;
        }

    }
}


