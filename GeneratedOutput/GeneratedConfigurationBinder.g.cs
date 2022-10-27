
namespace Microsoft.Extensions.Configuration
{
    public static class GeneratedConfigurationBinder
    {
        public static void Bind<T>(this Microsoft.Extensions.Configuration.IConfiguration configuration, T value)
        {
            if (typeof(T) == typeof(ConfigurationOptions))
            {
                Bind(configuration, (ConfigurationOptions)(object)value);
            }
            else if (typeof(T) == typeof(MyOptions))
            {
                Bind(configuration, (MyOptions)(object)value);
            }
        }

        static void Bind(Microsoft.Extensions.Configuration.IConfiguration configuration, ConfigurationOptions value)
        {
            value.Data ??= new();
            Bind(configuration.GetSection("Data"), value.Data);
            value.AzureAd ??= new();
            Bind(configuration.GetSection("AzureAd"), value.AzureAd);
            value.Redis ??= new();
            Bind(configuration.GetSection("Redis"), value.Redis);
            value.KeyVault ??= new();
            Bind(configuration.GetSection("KeyVault"), value.KeyVault);
        }
        static void Bind(Microsoft.Extensions.Configuration.IConfiguration configuration, MyOptions value)
        {
            value.A = int.TryParse(configuration["A"], out var ATemp) ? ATemp : default;
            value.B = int.TryParse(configuration["B"], out var BTemp) ? BTemp : default;
            value.S = configuration["S"];
            value.MyProperty2 = System.DateTime.TryParse(configuration["MyProperty2"], out var MyProperty2Temp) ? MyProperty2Temp : default;
            value.Data = System.Convert.FromBase64String(configuration["Data"]);
            value.Section = configuration as Microsoft.Extensions.Configuration.IConfigurationSection;
            // System.Collections.Generic.Dictionary<string, string> is currently unsupported
            value.Values = default;
            // System.Collections.Generic.List<MyClass> is currently unsupported
            value.Values2 = default;
            value.MyProperty ??= new();
            Bind(configuration.GetSection("MyProperty"), value.MyProperty);
        }
        static void Bind(Microsoft.Extensions.Configuration.IConfiguration configuration, DatabaseOptions value)
        {
            value.SurveysConnectionString = configuration["SurveysConnectionString"];
        }
        static void Bind(Microsoft.Extensions.Configuration.IConfiguration configuration, AzureAdOptions value)
        {
            {
                var items = new List<string>();
                foreach (var item in configuration.GetSection("ClientId").GetChildren())
                {
                    items.Add(item.Value);
                }
                value.ClientId = items.ToArray();
            }
            value.ClientSecret = configuration["ClientSecret"];
            value.PostLogoutRedirectUri = configuration["PostLogoutRedirectUri"];
            value.WebApiResourceId = configuration["WebApiResourceId"];
            value.Asymmetric ??= new();
            Bind(configuration.GetSection("Asymmetric"), value.Asymmetric);
        }
        static void Bind(Microsoft.Extensions.Configuration.IConfiguration configuration, RedisOptions value)
        {
            value.Configuration = configuration["Configuration"];
        }
        static void Bind(Microsoft.Extensions.Configuration.IConfiguration configuration, KeyVaultOptions value)
        {
            value.Name = configuration["Name"];
        }
        static void Bind(Microsoft.Extensions.Configuration.IConfiguration configuration, MyClass value)
        {
            value.SomethingElse = int.TryParse(configuration["SomethingElse"], out var SomethingElseTemp) ? SomethingElseTemp : default;
        }
        static void Bind(Microsoft.Extensions.Configuration.IConfiguration configuration, AsymmetricEncryptionOptions value)
        {
            value.CertificateThumbprint = configuration["CertificateThumbprint"];
            value.StoreName = Enum.TryParse<System.Security.Cryptography.X509Certificates.StoreName>(configuration["StoreName"], true, out var StoreNameTemp) ? StoreNameTemp : default;
            value.StoreLocation = Enum.TryParse<System.Security.Cryptography.X509Certificates.StoreLocation>(configuration["StoreLocation"], true, out var StoreLocationTemp) ? StoreLocationTemp : default;
            value.ValidationRequired = bool.TryParse(configuration["ValidationRequired"], out var ValidationRequiredTemp) ? ValidationRequiredTemp : default;
        }
    }
}