using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Roslyn.Reflection;

namespace Configuration.SourceGenerator
{
    internal class WellKnownTypes
    {
        public WellKnownTypes(MetadataLoadContext metadataLoadContext)
        {
            IConfigurationType = metadataLoadContext.ResolveType<IConfiguration>();
            IConfigurationSectionType = metadataLoadContext.ResolveType<IConfigurationSection>();
            IServiceCollectionType = metadataLoadContext.ResolveType<IServiceCollection>();
            GenerateBinderAttributeType = metadataLoadContext.ResolveType<GenerateBinderAttribute>();
        }

        public Type IConfigurationType { get; }
        public Type IConfigurationSectionType { get; }
        public Type IServiceCollectionType { get; }
        public Type GenerateBinderAttributeType { get; }
    }
}

namespace Microsoft.Extensions.Configuration
{
    interface IConfiguration { }
    interface IConfigurationSection { }
    class GenerateBinderAttribute { }
}

namespace Microsoft.Extensions.DependencyInjection
{
    interface IServiceCollection { }
}