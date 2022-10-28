using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Configuration.SourceGenerator
{
    internal class WellKnownTypes
    {
        public WellKnownTypes(MetadataLoadContext metadataLoadContext)
        {
            IConfigurationType = metadataLoadContext.Resolve<IConfiguration>();
            IConfigurationSectionType = metadataLoadContext.Resolve<IConfigurationSection>();
            IServiceCollectionType = metadataLoadContext.Resolve<IServiceCollection>();
        }

        public Type IConfigurationType { get; }
        public Type IConfigurationSectionType { get; }
        public Type IServiceCollectionType { get; }
    }
}

namespace Microsoft.Extensions.Configuration
{
    interface IConfiguration { }
    interface IConfigurationSection { }
}

namespace Microsoft.Extensions.DependencyInjection
{
    interface IServiceCollection { }
}