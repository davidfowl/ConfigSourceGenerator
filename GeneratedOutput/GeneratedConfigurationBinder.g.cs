
namespace Microsoft.Extensions.Configuration
{
    public static class GeneratedConfigurationBinder
    {
        public static void Bind<T>(this Microsoft.Extensions.Configuration.IConfiguration configuration, T value)
        {
            if (typeof(T) == typeof(ConfigurationOptions))
            {
                BindCore(configuration, (ConfigurationOptions)(object)value);
            }
            else if (typeof(T) == typeof(MyOptions))
            {
                BindCore(configuration, (MyOptions)(object)value);
            }
        }

        static void BindCore(Microsoft.Extensions.Configuration.IConfiguration configuration, ConfigurationOptions value)
        {
            value.Data ??= new();
            BindCore(configuration.GetSection("Data"), value.Data);
            value.AzureAd ??= new();
            BindCore(configuration.GetSection("AzureAd"), value.AzureAd);
            value.Redis ??= new();
            BindCore(configuration.GetSection("Redis"), value.Redis);
            value.KeyVault ??= new();
            BindCore(configuration.GetSection("KeyVault"), value.KeyVault);
        }

        static void BindCore(Microsoft.Extensions.Configuration.IConfiguration configuration, MyOptions value)
        {
            value.A = int.TryParse(configuration["A"], out var ATemp) ? ATemp : default;
            value.B = int.TryParse(configuration["B"], out var BTemp) ? BTemp : default;
            value.S = configuration["S"];
            value.MyProperty2 = System.DateTime.TryParse(configuration["MyProperty2"], out var MyProperty2Temp) ? MyProperty2Temp : default;
            value.Data = System.Convert.FromBase64String(configuration["Data"]);
            value.Section = configuration as Microsoft.Extensions.Configuration.IConfigurationSection;
            // System.Collections.Generic.Dictionary<string, string> is not supported
            value.Values = default;
            // System.Collections.Generic.List<MyClass> is not supported
            value.Values2 = default;
            {
                var items = new List<MyClass>();
                foreach (var item in configuration.GetSection("Values3").GetChildren())
                {
                    MyClass current = default;
                    current ??= new();
                    BindCore(item.GetSection(item.Key), current);
                    items.Add(current);
                }
                value.Values3 = items.ToArray();
            }
            {
                var items = new List<MyClass>();
                foreach (var item in configuration.GetSection("Values4").GetChildren())
                {
                    MyClass current = default;
                    current ??= new();
                    BindCore(item.GetSection(item.Key), current);
                    items.Add(current);
                }
                value.Values4 = items.ToArray();
            }
            {
                var dict = new System.Collections.Generic.Dictionary<int, MyClass>();
                foreach (var item in configuration.GetSection("Values5").GetChildren())
                {
                    MyClass current = default;
                    current ??= new();
                    BindCore(item.GetSection(item.Key), current);
                    dict.Add(int.TryParse(item.Key, out var key) ? key : throw new System.IO.InvalidDataException($"Unable to parse {item.Key}."), current);
                }
                value.Values5 = dict;
            }
            {
                var dict = new System.Collections.Generic.Dictionary<System.Security.Cryptography.X509Certificates.StoreName, MyClass>();
                foreach (var item in configuration.GetSection("Values6").GetChildren())
                {
                    MyClass current = default;
                    current ??= new();
                    BindCore(item.GetSection(item.Key), current);
                    dict.Add(Enum.TryParse<System.Security.Cryptography.X509Certificates.StoreName>(item.Key, true, out var key) ? key : throw new System.IO.InvalidDataException($"Unable to parse {item.Key}."), current);
                }
                value.Values6 = dict;
            }
            // System.Collections.Generic.IDictionary<MyClass, MyClass> is not supported
            value.Values7 = default;
            value.MyProperty ??= new();
            BindCore(configuration.GetSection("MyProperty"), value.MyProperty);
        }

        static void BindCore(Microsoft.Extensions.Configuration.IConfiguration configuration, DatabaseOptions value)
        {
            value.SurveysConnectionString = configuration["SurveysConnectionString"];
        }

        static void BindCore(Microsoft.Extensions.Configuration.IConfiguration configuration, AzureAdOptions value)
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
            BindCore(configuration.GetSection("Asymmetric"), value.Asymmetric);
        }

        static void BindCore(Microsoft.Extensions.Configuration.IConfiguration configuration, RedisOptions value)
        {
            value.Configuration = configuration["Configuration"];
        }

        static void BindCore(Microsoft.Extensions.Configuration.IConfiguration configuration, KeyVaultOptions value)
        {
            value.Name = configuration["Name"];
        }

        static void BindCore(Microsoft.Extensions.Configuration.IConfiguration configuration, MyClass value)
        {
            value.SomethingElse = int.TryParse(configuration["SomethingElse"], out var SomethingElseTemp) ? SomethingElseTemp : default;
        }

        static void BindCore(Microsoft.Extensions.Configuration.IConfiguration configuration, AsymmetricEncryptionOptions value)
        {
            value.CertificateThumbprint = configuration["CertificateThumbprint"];
            value.StoreName = Enum.TryParse<System.Security.Cryptography.X509Certificates.StoreName>(configuration["StoreName"], true, out var StoreNameTemp) ? StoreNameTemp : default;
            value.StoreLocation = Enum.TryParse<System.Security.Cryptography.X509Certificates.StoreLocation>(configuration["StoreLocation"], true, out var StoreLocationTemp) ? StoreLocationTemp : default;
            value.ValidationRequired = bool.TryParse(configuration["ValidationRequired"], out var ValidationRequiredTemp) ? ValidationRequiredTemp : default;
        }
    }
}