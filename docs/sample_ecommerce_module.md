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

## Product Export Example

`Atlas.Sample.WebApi` exposes a product CSV export sample:

```text
POST /api/product/exports
GET  /api/product/exports/{exportJobId}
GET  /api/product/exports/{exportJobId}/download
```

The request supports frontend-selected columns through server-validated field names:

```json
{
  "format": "csv",
  "selectedFields": [ "name", "price" ],
  "criteria": {
    "keyword": "implant",
    "minPrice": 10,
    "maxPrice": 200,
    "onlyCustomized": true
  }
}
```

The sample provider is registered by `SampleECommerceModule`, declares the allowed product columns on the server, and maps `criteria` to `IProductQueryService` for paged reads instead of opening a `DbContext` directly.

Current compatibility note: the historical sample entities and migrations remain in the existing Atlas entity/migration assemblies to avoid a data model namespace migration in this PR. New business modules should use `dotnet new atlas-module` and keep their registration in their own module project.
