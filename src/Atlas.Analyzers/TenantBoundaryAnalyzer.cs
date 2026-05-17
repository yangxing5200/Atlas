using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Atlas.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TenantBoundaryAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor ForbiddenTenantDataTypeRule = new(
        id: "ATL001",
        title: "Tenant data access must stay behind repository or infrastructure boundaries",
        messageFormat: "Do not reference '{0}' from this assembly. Use repository, query service, or an approved infrastructure service.",
        category: "Atlas.TenantIsolation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ForbiddenDbSetRule = new(
        id: "ATL002",
        title: "DbContext.Set must not be used from application or business code",
        messageFormat: "Do not call '{0}' from this assembly. Use repository/query service APIs so tenant and store scope is applied.",
        category: "Atlas.TenantIsolation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ForbiddenRawSqlRule = new(
        id: "ATL003",
        title: "Raw SQL must use an approved scoped executor",
        messageFormat: "Do not call '{0}' directly from this assembly. Use an approved SQL executor that enforces tenant scope.",
        category: "Atlas.TenantIsolation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            ForbiddenTenantDataTypeRule,
            ForbiddenDbSetRule,
            ForbiddenRawSqlRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            if (!ShouldEnforce(startContext.Compilation.AssemblyName))
                return;

            var tenantDbContextFactory = startContext.Compilation.GetTypeByMetadataName(
                "Atlas.Data.Tenant.Context.ITenantDbContextFactory");
            var tenantDbContext = startContext.Compilation.GetTypeByMetadataName(
                "Atlas.Data.Tenant.Context.AtlasTenantDbContext");
            var efDbContext = startContext.Compilation.GetTypeByMetadataName(
                "Microsoft.EntityFrameworkCore.DbContext");

            startContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeTypeReference(ctx, tenantDbContextFactory, tenantDbContext, efDbContext),
                SyntaxKind.Parameter,
                SyntaxKind.PropertyDeclaration,
                SyntaxKind.FieldDeclaration,
                SyntaxKind.VariableDeclaration);

            startContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeInvocation(ctx, efDbContext),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeTypeReference(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? tenantDbContextFactory,
        INamedTypeSymbol? tenantDbContext,
        INamedTypeSymbol? efDbContext)
    {
        var typeSyntax = context.Node switch
        {
            ParameterSyntax parameter => parameter.Type,
            PropertyDeclarationSyntax property => property.Type,
            FieldDeclarationSyntax field => field.Declaration.Type,
            VariableDeclarationSyntax variable => variable.Type,
            _ => null
        };

        if (typeSyntax == null)
            return;

        var type = context.SemanticModel.GetTypeInfo(typeSyntax, context.CancellationToken).Type;
        if (!IsForbiddenTenantDataType(type, tenantDbContextFactory, tenantDbContext, efDbContext))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            ForbiddenTenantDataTypeRule,
            typeSyntax.GetLocation(),
            type!.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? efDbContext)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol == null)
            return;

        if (symbol.Name == "Set" && InheritsFromOrEquals(symbol.ContainingType, efDbContext))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ForbiddenDbSetRule,
                invocation.GetLocation(),
                symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
            return;
        }

        if ((symbol.Name.StartsWith("ExecuteSql", StringComparison.Ordinal) ||
             symbol.Name.StartsWith("FromSql", StringComparison.Ordinal)) &&
            symbol.ContainingNamespace.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ForbiddenRawSqlRule,
                invocation.GetLocation(),
                symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
        }
    }

    private static bool IsForbiddenTenantDataType(
        ITypeSymbol? type,
        INamedTypeSymbol? tenantDbContextFactory,
        INamedTypeSymbol? tenantDbContext,
        INamedTypeSymbol? efDbContext)
    {
        if (type == null)
            return false;

        if (SymbolEqualityComparer.Default.Equals(type, tenantDbContextFactory) ||
            SymbolEqualityComparer.Default.Equals(type, tenantDbContext))
        {
            return true;
        }

        return InheritsFromOrEquals(type, efDbContext);
    }

    private static bool InheritsFromOrEquals(ITypeSymbol? type, INamedTypeSymbol? baseType)
    {
        if (type == null || baseType == null)
            return false;

        for (var current = type; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }

        return false;
    }

    private static bool ShouldEnforce(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return false;

        var name = assemblyName!;
        if (!name.StartsWith("Atlas.", StringComparison.Ordinal))
            return false;

        if (name.EndsWith(".Tests", StringComparison.Ordinal) ||
            name.StartsWith("Atlas.Data.", StringComparison.Ordinal) ||
            name is "Atlas.Analyzers" or "Atlas.LocalSetup" or
                "Atlas.Extensions.DependencyInjection" or "Atlas.Services.Tenant" or
                "Atlas.BackgroundTasks")
        {
            return false;
        }

        return true;
    }
}
