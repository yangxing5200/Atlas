migration script
dotnet ef migrations add v0.0.1 --project Atlas.Data.Tenant.Migrations --startup-project Atlas.Data.Tenant.Migrations --context AtlasTenantDbContext --output-dir Migrations
dotnet ef migrations add v0.0.2 --project Atlas.Data.Tenant.Migrations --startup-project Atlas.Data.Tenant.Migrations --context AtlasTenantDbContext --output-dir Migrations
dotnet ef migrations add v0.0.3-auto-id --project Atlas.Data.Tenant.Migrations --startup-project Atlas.Data.Tenant.Migrations --context AtlasTenantDbContext --output-dir Migrations

database update
dotnet ef database update --project Atlas.Data.Tenant.Migrations --startup-project Atlas.Data.Tenant.Migrations --context AtlasTenantDbContext