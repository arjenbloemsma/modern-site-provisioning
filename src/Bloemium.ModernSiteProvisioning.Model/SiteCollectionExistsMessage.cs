namespace Bloemium.ModernSiteProvisioning.Model
{
    public class SiteCollectionExistsMessage
    {
        public string Title { get; set; }
        public string Type { get; set; }
        public string AbsoluteUri { get; set; }
        public string RelativeUrl { get; set; }
        public bool Exists { get; set; }
    }
}
