using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace Configuration.SourceGenerator
{
    internal class WellKnownTypes
    {
        public WellKnownTypes(MetadataLoadContext metadataLoadContext)
        {
            IConfigurationType = metadataLoadContext.Resolve<IConfiguration>();
            IConfigurationSectionType = metadataLoadContext.Resolve<IConfigurationSection>();
        }

        public Type IConfigurationType { get; }
        public Type IConfigurationSectionType { get; }
    }
}

namespace Microsoft.Extensions.Configuration
{
    interface IConfiguration { }
    interface IConfigurationSection { }
}