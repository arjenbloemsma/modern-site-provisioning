namespace Bloemium.ModernSiteProvisioning.Model
{
    public class ApplyProvisioningTemplateJob
    {
        public int ListItemID { get; set; }
        public string FileNameWithExtension { get; set; }
        public string ProvisioningTemplateUrl { get; set; }
        public bool Checked { get; set; } = false;
    }
}
