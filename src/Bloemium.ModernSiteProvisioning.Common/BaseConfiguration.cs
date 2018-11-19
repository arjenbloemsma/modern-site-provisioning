using Microsoft.Azure;
using System;

namespace Bloemium.ModernSiteProvisioning.Common
{
    public class BaseConfiguration : IConfiguration
    {
        public string GetSetting(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return CloudConfigurationManager.GetSetting(name);
        }
    }
}