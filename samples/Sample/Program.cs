using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

var builder = WebApplication.CreateBuilder(args);

var section = builder.Configuration.GetSection("MyOptions");

builder.Services.Configure<MyOptions>(section);

var myOptions0 = section.Get<MyOptions>();

var myOptions1 = new MyOptions();
section.Bind(myOptions1);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();

class MyClass2
{
    public MyClass2(IConfiguration c)
    {
        c.Bind(this);
    }
}

[GenerateBinder]
class MyOptions
{
    public int A { get; set; }
    public int B { get; set; }
    public string S { get; set; }

    public int MyProperty1 { get; }

    public DateTime MyProperty2 { get; set; }

    public byte[] Data { get; set; }

    public IConfigurationSection Section { get; set; }

    public Dictionary<string, string> Values { get; set; }
    public List<MyClass> Values2 { get; set; }
    public MyClass[] Values3 { get; set; }
    public IEnumerable<MyClass> Values4 { get; set; }

    public IDictionary<int, MyClass> Values5 { get; set; }
    public IDictionary<StoreName, MyClass> Values6 { get; set; }
    public IDictionary<MyClass, MyClass> Values7 { get; set; }

    public MyClass MyProperty { get; set; }
}

public class MyClass
{
    public int SomethingElse { get; set; }
}

[GenerateBinder]
public class ConfigurationOptions
{
    public ConfigurationOptions()
    {
        Data = new DatabaseOptions();
        Redis = new RedisOptions();
        KeyVault = new KeyVaultOptions();
        AzureAd = new AzureAdOptions();
    }
    public DatabaseOptions Data { get; set; }
    public AzureAdOptions AzureAd { get; set; }
    public RedisOptions Redis { get; set; }
    public KeyVaultOptions KeyVault { get; set; }
}

public class DatabaseOptions
{
    public string SurveysConnectionString { get; set; }
}

public class AzureAdOptions
{
    public AzureAdOptions()
    {
        Asymmetric = new AsymmetricEncryptionOptions();
    }
    public string[] ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string PostLogoutRedirectUri { get; set; }
    public string WebApiResourceId { get; set; }

    public AsymmetricEncryptionOptions Asymmetric { get; set; }

}

public class KeyVaultOptions
{
    public string Name { get; set; }
}

public class SurveyApiOptions
{
    public string Scopes { get; set; }

    public string BaseUrl { get; set; }

    public string Name { get; set; }
}
public class RedisOptions
{
    public string Configuration { get; set; }
}

public class AsymmetricEncryptionOptions
{
    public AsymmetricEncryptionOptions()
    {
        StoreName = StoreName.My;
        StoreLocation = StoreLocation.CurrentUser;
        ValidationRequired = false;
    }
    public string CertificateThumbprint { get; set; }
    public StoreName StoreName { get; set; }
    public StoreLocation StoreLocation { get; set; }
    public bool ValidationRequired { get; set; }
}