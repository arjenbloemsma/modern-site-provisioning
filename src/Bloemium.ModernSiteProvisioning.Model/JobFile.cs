namespace Bloemium.ModernSiteProvisioning.Model
{
    public class JobFile
    {
        public string RelativeUrl { get; set; }
        public string SiteTitle { get; set; }
        public string Owner { get; set; }
        public int StorageMaximumLevel { get; set; }
        public int StorageWarningLevel { get; set; }
        public int UserCodeMaximumLevel { get; set; }
        public int UserCodeWarningLevel { get; set; }
        public int TimeZone { get; set; }
    }
}