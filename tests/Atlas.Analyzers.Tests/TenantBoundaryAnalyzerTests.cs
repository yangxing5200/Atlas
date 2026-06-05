using System.Collections.Immutable;
using Atlas.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Atlas.Analyzers.Tests;

public sealed class TenantBoundaryAnalyzerTests
{
    [Fact]
    public async Task Repository_usage_in_business_assembly_is_allowed()
    {
        var diagnostics = await AnalyzeAsync(
            """
            using System.Threading.Tasks;

            namespace Atlas.Data.Abstractions
            {
                public interface IRepository<TEntity>
                {
                    Task<TEntity?> GetByIdAsync(long id);
                }
            }

            namespace Atlas.Services
            {
                using Atlas.Data.Abstractions;

                public sealed class Product
                {
                    public long Id { get; set; }
                }

                public sealed class ProductService
                {
                    private readonly IRepository<Product> _repository;

                    public ProductService(IRepository<Product> repository)
                    {
                        _repository = repository;
                    }

                    public Task<Product?> GetAsync(long id)
                    {
                        return _repository.GetByIdAsync(id);
                    }
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Direct_tenant_db_context_reference_reports_atl001()
    {
        var diagnostics = await AnalyzeAsync(
            TenantInfrastructureSource +
            """

            namespace Atlas.Services
            {
                using Atlas.Data.Tenant.Context;

                public sealed class BadService
                {
                    public BadService(AtlasTenantDbContext db)
                    {
                    }
                }
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "ATL001");
    }

    [Fact]
    public async Task Direct_tenant_db_context_factory_reference_reports_atl001()
    {
        var diagnostics = await AnalyzeAsync(
            TenantInfrastructureSource +
            """

            namespace Atlas.Services
            {
                using Atlas.Data.Tenant.Context;

                public sealed class BadService
                {
                    public BadService(ITenantDbContextFactory factory)
                    {
                    }
                }
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "ATL001");
    }

    [Fact]
    public async Task Db_context_set_call_reports_atl002()
    {
        var diagnostics = await AnalyzeAsync(
            TenantInfrastructureSource +
            """

            namespace Atlas.Services
            {
                using Atlas.Data.Tenant.Context;

                public sealed class Product
                {
                    public long Id { get; set; }
                }

                public sealed class BadService
                {
                    public object Query(AtlasTenantDbContext db)
                    {
                        return db.Set<Product>();
                    }
                }
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "ATL002");
    }

    [Fact]
    public async Task Execute_sql_call_reports_atl003()
    {
        var diagnostics = await AnalyzeAsync(
            TenantInfrastructureSource +
            """

            namespace Atlas.Services
            {
                using Atlas.Data.Tenant.Context;
                using Microsoft.EntityFrameworkCore;

                public sealed class BadService
                {
                    public void DeleteRows(AtlasTenantDbContext db)
                    {
                        db.Database.ExecuteSqlRaw("delete from Products");
                    }
                }
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "ATL003");
    }

    [Fact]
    public async Task From_sql_call_reports_atl003()
    {
        var diagnostics = await AnalyzeAsync(
            TenantInfrastructureSource +
            """

            namespace Atlas.Services
            {
                using Atlas.Data.Tenant.Context;
                using Microsoft.EntityFrameworkCore;

                public sealed class Product
                {
                    public long Id { get; set; }
                }

                public sealed class BadService
                {
                    public object Query(AtlasTenantDbContext db)
                    {
                        return db.Set<Product>().FromSqlRaw("select * from Products");
                    }
                }
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "ATL003");
    }

    [Theory]
    [InlineData("Atlas.Data.Tenant")]
    [InlineData("Atlas.BackgroundTasks")]
    [InlineData("Atlas.Exporting")]
    public async Task Approved_infrastructure_assemblies_are_not_reported(string assemblyName)
    {
        var diagnostics = await AnalyzeAsync(
            TenantInfrastructureSource +
            """

            namespace Atlas.InfrastructureRuntime
            {
                using Atlas.Data.Tenant.Context;
                using Microsoft.EntityFrameworkCore;

                public sealed class Product
                {
                    public long Id { get; set; }
                }

                public sealed class ApprovedRuntimeService
                {
                    private readonly AtlasTenantDbContext _db;

                    public ApprovedRuntimeService(AtlasTenantDbContext db)
                    {
                        _db = db;
                    }

                    public object Query()
                    {
                        return _db.Set<Product>().FromSqlRaw("select * from Products");
                    }
                }
            }
            """,
            assemblyName);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Tenant_runtime_namespace_is_not_reported()
    {
        var diagnostics = await AnalyzeAsync(
            TenantInfrastructureSource +
            """

            namespace Atlas.Services.Tenant.Runtime.Messaging
            {
                using Atlas.Data.Tenant.Context;
                using Microsoft.EntityFrameworkCore;

                public sealed class Product
                {
                    public long Id { get; set; }
                }

                public sealed class ApprovedRuntimeService
                {
                    private readonly AtlasTenantDbContext _db;

                    public ApprovedRuntimeService(AtlasTenantDbContext db)
                    {
                        _db = db;
                    }

                    public object Query()
                    {
                        return _db.Set<Product>().FromSqlRaw("select * from Products");
                    }
                }
            }
            """,
            "Atlas.Services.Tenant");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Tenant_business_namespace_reports_direct_db_context()
    {
        var diagnostics = await AnalyzeAsync(
            TenantInfrastructureSource +
            """

            namespace Atlas.Services.Tenant
            {
                using Atlas.Data.Tenant.Context;

                public sealed class BadTenantService
                {
                    public BadTenantService(AtlasTenantDbContext db)
                    {
                    }
                }
            }
            """,
            "Atlas.Services.Tenant");

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "ATL001");
    }

    [Fact]
    public async Task Query_service_with_direct_db_context_reports_boundary_diagnostics()
    {
        var diagnostics = await AnalyzeAsync(
            TenantInfrastructureSource +
            """

            namespace Atlas.Services.Queries
            {
                using Atlas.Data.Tenant.Context;

                public sealed class Product
                {
                    public long Id { get; set; }
                }

                public sealed class BadProductQueryService
                {
                    private readonly AtlasTenantDbContext _db;

                    public BadProductQueryService(AtlasTenantDbContext db)
                    {
                        _db = db;
                    }

                    public object Search()
                    {
                        return _db.Set<Product>();
                    }
                }
            }
            """);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "ATL001");
        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "ATL002");
    }

    [Fact]
    public async Task Http_action_without_permission_policy_reports_atl004()
    {
        var diagnostics = await AnalyzeAsync(
            AuthorizationMvcSource +
            """

            namespace Atlas.Sample.WebApi.Controllers
            {
                using Microsoft.AspNetCore.Authorization;
                using Microsoft.AspNetCore.Mvc;

                public sealed class ProductsController
                {
                    [Authorize]
                    [HttpGet]
                    public string Search() => "";
                }
            }
            """,
            "Atlas.Sample.WebApi");

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "ATL004");
    }

    [Fact]
    public async Task Http_action_with_permission_policy_is_allowed()
    {
        var diagnostics = await AnalyzeAsync(
            AuthorizationMvcSource +
            """

            namespace Atlas.Sample.WebApi.Controllers
            {
                using Microsoft.AspNetCore.Authorization;
                using Microsoft.AspNetCore.Mvc;

                public sealed class ProductsController
                {
                    [Authorize(Policy = "Permission:products.read")]
                    [HttpGet]
                    public string Search() => "";
                }
            }
            """,
            "Atlas.Sample.WebApi");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "ATL004");
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "ATL005");
    }

    [Fact]
    public async Task Http_action_with_allow_anonymous_is_allowed()
    {
        var diagnostics = await AnalyzeAsync(
            AuthorizationMvcSource +
            """

            namespace Atlas.Sample.WebApi.Controllers
            {
                using Microsoft.AspNetCore.Authorization;
                using Microsoft.AspNetCore.Mvc;

                public sealed class UsersController
                {
                    [AllowAnonymous]
                    [HttpPost]
                    public string Login() => "";
                }
            }
            """,
            "Atlas.Sample.WebApi");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "ATL004");
    }

    [Fact]
    public async Task Package_style_permission_policy_reports_atl005()
    {
        var diagnostics = await AnalyzeAsync(
            AuthorizationMvcSource +
            """

            namespace Atlas.Sample.WebApi.Controllers
            {
                using Microsoft.AspNetCore.Authorization;
                using Microsoft.AspNetCore.Mvc;

                public sealed class ProductsController
                {
                    [Authorize(Policy = "Permission:package.standard")]
                    [HttpGet]
                    public string Search() => "";
                }
            }
            """,
            "Atlas.Sample.WebApi");

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "ATL005");
    }

    private const string TenantInfrastructureSource =
        """
        namespace Atlas.Data.Tenant.Context
        {
            public interface ITenantDbContextFactory
            {
            }

            public sealed class AtlasTenantDbContext : Microsoft.EntityFrameworkCore.DbContext
            {
            }
        }
        """;

    private const string AuthorizationMvcSource =
        """
        namespace Microsoft.AspNetCore.Authorization
        {
            public sealed class AuthorizeAttribute : System.Attribute
            {
                public string? Policy { get; set; }
            }

            public sealed class AllowAnonymousAttribute : System.Attribute
            {
            }
        }

        namespace Microsoft.AspNetCore.Mvc
        {
            public sealed class HttpGetAttribute : System.Attribute
            {
            }

            public sealed class HttpPostAttribute : System.Attribute
            {
            }
        }
        """;

    private static async Task<IReadOnlyList<Diagnostic>> AnalyzeAsync(
        string source,
        string assemblyName = "Atlas.Services")
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest)) },
            CreateReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compilerErrors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(compilerErrors);

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new TenantBoundaryAnalyzer());
        var analyzerDiagnostics = await compilation
            .WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync();

        return analyzerDiagnostics
            .Where(diagnostic => diagnostic.Id.StartsWith("ATL", StringComparison.Ordinal))
            .OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            ?? Array.Empty<string>();

        var outputAssemblies = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll");

        return trustedPlatformAssemblies
            .Concat(outputAssemblies)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToList();
    }
}
