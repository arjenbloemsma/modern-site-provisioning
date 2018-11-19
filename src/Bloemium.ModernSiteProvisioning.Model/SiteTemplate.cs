using OfficeDevPnP.Core.Framework.Provisioning.Connectors;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.Providers.Xml;
using System;
using System.IO;
using System.Linq;

namespace Bloemium.ModernSiteProvisioning.Model
{
    public class SiteTemplate : IDisposable
    {
        private bool disposed = false;
        private Stream stream;
        private Lazy<ProvisioningTemplate> provisioningTemplate;

        public ProvisioningTemplate ProvisioningTemplate {
            get {
                if (this.disposed)
                {
                    throw new ObjectDisposedException(this.GetType().FullName);
                }

                return this.provisioningTemplate.Value;
            }

        }

        public SiteTemplate(Stream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException();
            this.provisioningTemplate = new Lazy<ProvisioningTemplate>(() => this.GetProvisioningTemplate());
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.stream != null)
                {
                    stream.Dispose();
                }

                this.disposed = true;
            }
        }

        private ProvisioningTemplate GetProvisioningTemplate()
        {
            var connector = new OpenXMLConnector(stream);
            var templateProvider = new XMLOpenXMLTemplateProvider(connector);

            var templates = templateProvider.GetTemplates();
            var template = templates.Single();
            template.Connector = connector;

            return template;
        }
    }
}