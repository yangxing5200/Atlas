# Sample ECommerce Module

`Atlas.Sample.ECommerce` owns the sample Product/Order registrations used by `Atlas.Sample.WebApi`.

The built-in Atlas module now registers platform services only. Product and order demo services are opt-in through:

```csharp
builder.AddAtlasWebApi(options =>
{
    options.ApiTitle = "Atlas Sample API";
}, modules =>
{
    modules.AddModule<SampleECommerceModule>();
});
```

This keeps framework startup from depending on demo business services while preserving the sample API behavior.

Current compatibility note: the historical sample entities and migrations remain in the existing Atlas entity/migration assemblies to avoid a data model namespace migration in this PR. New business modules should use `dotnet new atlas-module` and keep their registration in their own module project.
