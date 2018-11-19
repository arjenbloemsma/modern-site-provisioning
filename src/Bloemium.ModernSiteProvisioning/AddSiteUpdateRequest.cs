using Bloemium.ModernSiteProvisioning.Model;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Bloemium.ModernSiteProvisioning
{
    public static class AddSiteUpdateRequest
    {
        private const string FunctionName = "add-site-update-request";

        [FunctionName(FunctionName)]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function,"post", Route = null)]HttpRequestMessage req,
            [ServiceBus("site-operations-topic", Connection = "ManageTopicConnection")]ICollector<BrokeredMessage> siteOperationsTopic,
            TraceWriter log)
        {
            log.Info($"C# HTTP trigger function '{FunctionName}' processed a request.");

            var contentType = req.Content.Headers.ContentType;
            var supportedMediaType = new MediaTypeHeaderValue("application/json");

            if (contentType == null || contentType.MediaType != supportedMediaType.MediaType)
            {
                var unsuportedMediaTypeMessage = $"Unsuported media type. Supported media type is {supportedMediaType.MediaType}";
                var response = req.CreateErrorResponse(HttpStatusCode.UnsupportedMediaType, unsuportedMediaTypeMessage);
                return response;
            }

            var bodyContent = req.Content.ReadAsStringAsync().Result;
            var updateMetadataRequest = JsonConvert.DeserializeObject<UpdateSiteRequest>(bodyContent);

            foreach (UpdateSiteJob updateMetadataJob in updateMetadataRequest.Sites)
            {
                var msg = new BrokeredMessage(updateMetadataJob)
                {
                    Label = updateMetadataRequest.Type
                };
                siteOperationsTopic.Add(msg);
            }

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
