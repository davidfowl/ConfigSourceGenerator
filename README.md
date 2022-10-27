## Configuration Source generator

The configuration source generator generates a Bind overload that replaces the built in ConfigurationBinder.Bind method in `Microsoft.Extensions.ConfigurationBinder`. This makes configuration binding trim and AOT friendly.

## Using CI Builds
To use CI builds add the following nuget feed:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear />
        <add key="ucontroller" value="https://f.feedz.io/davidfowl/ucontroller/nuget/index.json" />
        <add key="NuGet.org" value="https://api.nuget.org/v3/index.json" />
    </packageSources>
</configuration>
```
