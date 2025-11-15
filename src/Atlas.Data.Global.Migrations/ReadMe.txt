migration script
dotnet ef migrations add v0.0.1 --project Atlas.Data.Global.Migrations --startup-project Atlas.Data.Global.Migrations --context AtlasGlobalDbContext --output-dir Migrations

database update
dotnet ef database update --project Atlas.Data.Global.Migrations --startup-project Atlas.Data.Global.Migrations --context AtlasGlobalDbContext