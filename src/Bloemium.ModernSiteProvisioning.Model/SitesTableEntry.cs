using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace Bloemium.ModernSiteProvisioning.Model
{
    public class SitesTableEntry : TableEntity
    {
        public string Title { get; set; }
        public string URL { get; set; }
        public int ProvisioningStatus { get; set; }
        public DateTime? Updated { get; set; }
        public string ProvisioningTemplateUrl { get; set; }
    }
}
