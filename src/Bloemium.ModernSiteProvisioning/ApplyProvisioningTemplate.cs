using Bloemium.ModernSiteProvisioning.Common;
using Bloemium.ModernSiteProvisioning.Common.Security;
using Bloemium.ModernSiteProvisioning.Common.SharePoint;
using Bloemium.ModernSiteProvisioning.Model;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Microsoft.SharePoint.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;

namespace Bloemium.ModernSiteProvisioning
{
    public static class ApplyProvisioningTemplate
    {
        private const string FunctionName = "apply-provisioning-template";

        [FunctionName(FunctionName)]
        public static void Run(
            [ServiceBusTrigger("update-sites-topic", "apply-template-subscription", AccessRights.Manage, Connection = "ManageTopicConnection")]BrokeredMessage updateMsg,
            [ServiceBus("new-sites-topic", Connection = "ManageTopicConnection")]ICollector<BrokeredMessage> newSitesTopic,
            ExecutionContext executionContext,
            TraceWriter log)
        {
            log.Info($"C# Service Bus trigger function '{FunctionName}' processed message: {updateMsg.MessageId} (Label': {updateMsg.Label}')");

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
            CloudBlobContainer container = blobClient.GetContainerReference(CloudConfigurationManager.GetSetting("JobFilesContainer"));

            var blob = container.GetBlobReference(applyProvisioningTemplateJob.FileNameWithExtension);
            var blobStream = new MemoryStream();
            blob.DownloadToStream(blobStream);
            streamReader = new StreamReader(blobStream);
            blobStream.Position = 0;
            string blobContent = streamReader.ReadToEnd();

            JObject provisioningJobFile = JObject.Parse(blobContent);
            var provisioningTemplateUrl = provisioningJobFile["ProvisioningTemplateUrl"].Value<string>();
            var relativeUrl = provisioningJobFile["RelativeUrl"].Value<string>();
            // get JSON result objects into a list
            IList<JToken> parameters = provisioningJobFile["TemplateParameters"].Children().ToList();
            // serialize JSON results into .NET objects
            IDictionary<string, string> templateParameters = new Dictionary<string, string>();
            foreach (JProperty parameter in parameters)
            {
                templateParameters.Add(parameter.Name, parameter.Value.ToObject<string>());
            }

            var clientContextManager = new ClientContextManager(new BaseConfiguration(), new CertificateManager());
            var provisioningSiteUrl = CloudConfigurationManager.GetSetting("ProvisioningSite");
            using (var ctx = clientContextManager.GetAzureADAppOnlyAuthenticatedContext(provisioningSiteUrl))
            {
                // Todo: get list title from configuration.
                // Assume that the web has a list named "PnPProvisioningJobs". 
                List provisioningJobsList = ctx.Web.Lists.GetByTitle("PnPProvisioningJobs");
                ListItem listItem = provisioningJobsList.GetItemById(applyProvisioningTemplateJob.ListItemID);
                // Write a new value to the PnPProvisioningJobStatus field of
                // the PnPProvisioningJobs item.
                listItem["PnPProvisioningJobStatus"] = "Running (applying template)";
                listItem.Update();
                ctx.ExecuteQuery();

                var templateContainer = blobClient.GetContainerReference(CloudConfigurationManager.GetSetting("TemplateFilesContainer"));
                var templateFileName = Path.GetFileName(provisioningTemplateUrl);
                var templateBlob = templateContainer.GetBlobReference(templateFileName);
                var templateBlobStream = new MemoryStream();
                templateBlob.DownloadToStream(templateBlobStream);
                var provisioningTemplate = new SiteTemplate(templateBlobStream).ProvisioningTemplate;
                log.Info($"(id {executionContext.InvocationId}) Retrieved template {templateFileName} from blob storage.");

                foreach (var parameter in templateParameters)
                {
                    provisioningTemplate.Parameters[parameter.Key] = parameter.Value;
                }

                var ptai = new ProvisioningTemplateApplyingInformation
                {
                    ProgressDelegate = (string message, int progress, int total) =>
                    {
                        log.Info($"(id {executionContext.InvocationId})[Progress]: {progress:00}/{total:00} - {message}");
                    },
                    MessagesDelegate = (string message, ProvisioningMessageType messageType) =>
                    {
                        log.Info($"(id {executionContext.InvocationId})[{messageType.ToString()}]: {message}");
                    },
                };
                var tenantUrl = new Uri(CloudConfigurationManager.GetSetting("TenantUrl"));
                Uri.TryCreate(tenantUrl, relativeUrl, out Uri fullSiteUrl);
                var templateAppliedWithOutAnyErrors = false;
                log.Info($"Opening ctx to {fullSiteUrl.AbsoluteUri}");
                using (var newSiteContext = clientContextManager.GetAzureADAppOnlyAuthenticatedContext(fullSiteUrl.AbsoluteUri))
                {
                    int tryCount = 0;
                    const int maxTries = 3;
                    do
                    {
                        tryCount++;
                        try
                        {
                            log.Info($"Applying the provisioning template {provisioningTemplateUrl} to {fullSiteUrl.AbsoluteUri}.");
                            newSiteContext.Web.ApplyProvisioningTemplate(provisioningTemplate, ptai);
                            log.Info($"Provisioning template has been applied to {fullSiteUrl.AbsoluteUri}.");
                            templateAppliedWithOutAnyErrors = true;
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Error occured while applying the provisioning template to {fullSiteUrl.AbsoluteUri}.", ex);
                            templateAppliedWithOutAnyErrors = false;
                            if (tryCount <= maxTries)
                            {
                                log.Warning($"An error occured while applying the provisioning template, but will try to apply the provisioning template to {fullSiteUrl.AbsoluteUri} once more. (max {maxTries} times, this was attempt number {tryCount}.)");
                            } else {
                                log.Warning($"Tried {maxTries} times to apply the provisioning template without succes.");
                            }
                        }
                    } while (templateAppliedWithOutAnyErrors == false && tryCount <= maxTries);
                }

                if (templateAppliedWithOutAnyErrors == true)
                {
                    var setDefaultColumnValuesMsg = new BrokeredMessage(applyProvisioningTemplateJob,
                        new DataContractJsonSerializer(typeof(ApplyProvisioningTemplateJob)))
                    {
                        ContentType = "application/json",
                        Label = "SetDefaultColumnValues"
                    };
                    newSitesTopic.Add(setDefaultColumnValuesMsg);
                    listItem["PnPProvisioningJobStatus"] = "Provisioned";
                } else
                {
                    listItem["PnPProvisioningJobStatus"] = "Failed (error while applying template)";
                }
                listItem.Update();
                ctx.ExecuteQuery();
            }
        }
    }
}