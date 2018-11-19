using System;

namespace Bloemium.ModernSiteProvisioning.Model
{
    public class MonitorSiteCollectionCreationData
    {
        public int ListItemID { get; set; }
        public string FullSiteUrl { get; set; }
        public string ProvisioningTemplateUrl { get; set; }
        public DateTime TimeStamp { get; set; }
        public CreateSiteCollectionJob CreateSiteCollectionJob { get; set; }
    }
}
