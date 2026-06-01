using Atlas.Extensions.DependencyInjection;
using Atlas.Services.Tenant.Runtime.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var command = GetCommand(args);
if (command is "help" or "--help" or "-h")
{
    PrintUsage();
    return;
}

var builder = Host.CreateApplicationBuilder(args);

// MigrationJob never owns HTTP or background execution planes.
builder.Configuration["Atlas:Runtime:Mode"] = "Migration";
builder.Configuration["Messaging:Provider"] ??= "None";
builder.Configuration["CacheSettings:Provider"] ??= "Memory";
builder.Configuration["ConnectionStrings:AtlasGlobal"] ??=
    "Server=localhost;Port=3306;Database=atlas_global;User=root;Password=root;CharSet=utf8mb4;AllowPublicKeyRetrieval=true;";

builder.Services.AddAtlasCore(builder.Configuration);

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var migrationService = scope.ServiceProvider.GetRequiredService<ITenantSchemaMigrationService>();

switch (command)
{
    case "plan":
    case "dry-run":
        await PrintPlanAsync(migrationService, host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
        break;
    case "apply":
        await ApplyAsync(migrationService, args, host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
        break;
    default:
        throw new InvalidOperationException("Unknown command. Use plan, dry-run, apply, or help.");
}

static async Task PrintPlanAsync(
    ITenantSchemaMigrationService migrationService,
    CancellationToken ct)
{
    var plan = await migrationService.BuildPlanAsync(ct);
    Console.WriteLine($"Tenant migration plan: {plan.Count} tenant(s).");

    foreach (var item in plan)
    {
        var pending = item.PendingMigrations.Count == 0
            ? "none"
            : string.Join(",", item.PendingMigrations);
        Console.WriteLine(
            $"tenant={item.TenantId}, current={item.CurrentVersion ?? "<none>"}, target={item.TargetVersion ?? "<none>"}, pending={pending}");
    }
}

static async Task ApplyAsync(
    ITenantSchemaMigrationService migrationService,
    string[] args,
    CancellationToken ct)
{
    var result = await migrationService.ExecuteAsync(
        new TenantSchemaMigrationOptions(
            DryRun: HasFlag(args, "--dry-run"),
            TenantBatchSize: GetIntOption(args, "--batch-size") ?? 100),
        ct);

    Console.WriteLine(
        $"Tenant migration completed. dryRun={result.DryRun}, succeeded={result.Succeeded}, skipped={result.Skipped}, failed={result.Failed}.");

    foreach (var item in result.Results)
    {
        Console.WriteLine(
            $"tenant={item.TenantId}, status={item.Status}, current={item.CurrentVersion ?? "<none>"}, target={item.TargetVersion ?? "<none>"}");
    }

    if (result.Failed > 0)
        Environment.ExitCode = 1;
}

static string GetCommand(string[] args)
{
    return args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal))
        ?.Trim()
        .ToLowerInvariant() ?? "plan";
}

static bool HasFlag(string[] args, string name)
{
    return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
}

static int? GetIntOption(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(args[i + 1], out var value))
        {
            return value;
        }
    }

    return null;
}

static void PrintUsage()
{
    Console.WriteLine("""
Atlas.MigrationJob commands:
  plan                 Print pending tenant schema migrations without applying changes.
  dry-run              Alias of plan.
  apply                Apply tenant schema migrations and update Global migration status.

Options:
  --dry-run            With apply, build the execution result without changing tenant databases.
  --batch-size <n>     Number of tenants scanned per batch. Default: 100.
""");
}
