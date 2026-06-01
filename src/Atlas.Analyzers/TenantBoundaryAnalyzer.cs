using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Atlas.Analyzers;

/// <summary>
/// 在编译期强制执行 Atlas 的租户数据访问边界。
/// </summary>
/// <remarks>
/// 该 Analyzer 主要约束面向应用层的程序集，阻止数据层/基础设施层之外的代码直接引用租户 EF 底层类型。
/// 直接访问 DbContext 容易绕过 repository/query service 中统一施加的租户和门店作用域，带来跨租户读写风险。
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TenantBoundaryAnalyzer : DiagnosticAnalyzer
{
    // ATL001 用来捕获对租户数据库底层类型的直接引用，包括构造函数参数、字段、属性和局部变量。
    // 只要语义类型解析为 AtlasTenantDbContext、ITenantDbContextFactory 或 EF Core DbContext，就会报错。
    private static readonly DiagnosticDescriptor ForbiddenTenantDataTypeRule = new(
        id: "ATL001",
        title: "Tenant data access must stay behind repository or infrastructure boundaries",
        messageFormat: "Do not reference '{0}' from this assembly. Use repository, query service, or an approved infrastructure service.",
        category: "Atlas.TenantIsolation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ATL002 用来捕获 DbContext.Set<T>() 调用。即使派生 DbContext 被声明成基类类型，
    // 语义分析仍能识别该调用所在类型继承自 EF Core DbContext。
    private static readonly DiagnosticDescriptor ForbiddenDbSetRule = new(
        id: "ATL002",
        title: "DbContext.Set must not be used from application or business code",
        messageFormat: "Do not call '{0}' from this assembly. Use repository/query service APIs so tenant and store scope is applied.",
        category: "Atlas.TenantIsolation",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ATL003 用来捕获对 EF raw SQL API 的直接调用。原生 SQL 必须经过受控的 scoped executor，
    // 这样租户过滤、参数化规则和审计要求才能集中在一条被认可的路径上。
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

    /// <summary>
    /// 注册 Analyzer 使用的语法节点回调。
    /// </summary>
    /// <remarks>
    /// Analyzer 会在每次编译开始时解析一次关键类型符号，然后在每个语法节点回调中复用这些符号。
    /// 这样每个节点回调保持简单，同时仍然基于语义信息检查，而不是靠字符串匹配。
    /// </remarks>
    public override void Initialize(AnalysisContext context)
    {
        // 生成文件里经常包含框架或工具生成的 EF 访问代码，这些代码不代表业务架构边界信号，
        // 因此这里完全跳过 generated code。
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // Roslyn 可能并行分析多个语法树。当前 Analyzer 只读取不可变的编译状态，
        // 不维护共享可变状态，因此可以安全启用并发执行。
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            // 只约束那些应该通过 Atlas 抽象访问租户数据的程序集。
            // 数据层和被批准的基础设施层会在 ShouldEnforce 中被明确排除。
            if (!ShouldEnforce(startContext.Compilation.AssemblyName))
                return;

            // 解析定义边界的关键类型符号。使用 metadata name 可以避免 using alias、
            // 完全限定名等写法绕过检查。
            var tenantDbContextFactory = startContext.Compilation.GetTypeByMetadataName(
                "Atlas.Data.Tenant.Context.ITenantDbContextFactory");
            var tenantDbContext = startContext.Compilation.GetTypeByMetadataName(
                "Atlas.Data.Tenant.Context.AtlasTenantDbContext");
            var efDbContext = startContext.Compilation.GetTypeByMetadataName(
                "Microsoft.EntityFrameworkCore.DbContext");

            // 检查会把禁用数据访问类型引入业务代码的声明位置。
            startContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeTypeReference(ctx, tenantDbContextFactory, tenantDbContext, efDbContext),
                SyntaxKind.Parameter,
                SyntaxKind.PropertyDeclaration,
                SyntaxKind.FieldDeclaration,
                SyntaxKind.VariableDeclaration);

            // 检查禁用 EF API 的调用点，包括通过 DbContext 基类暴露出来的调用。
            startContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeInvocation(ctx, efDbContext),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>
    /// 当声明引入了禁用的租户数据访问类型时，报告 ATL001。
    /// </summary>
    private static void AnalyzeTypeReference(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? tenantDbContextFactory,
        INamedTypeSymbol? tenantDbContext,
        INamedTypeSymbol? efDbContext)
    {
        if (IsApprovedRuntimeLocation(context))
            return;

        // 该回调注册在多种声明节点上。这里先把不同声明形态统一提取为 type syntax，
        // 这样下面的语义检查逻辑可以复用。
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

        // 使用语义类型信息而不是源码文本。这样可以识别 using alias、完全限定名、
        // 继承自 DbContext 的类型，以及通过 using 指令引入的类型。
        var type = context.SemanticModel.GetTypeInfo(typeSyntax, context.CancellationToken).Type;
        if (!IsForbiddenTenantDataType(type, tenantDbContextFactory, tenantDbContext, efDbContext))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            ForbiddenTenantDataTypeRule,
            typeSyntax.GetLocation(),
            type!.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    /// <summary>
    /// 当调用点使用了禁用的 EF Core API 时，报告 ATL002 或 ATL003。
    /// </summary>
    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? efDbContext)
    {
        if (IsApprovedRuntimeLocation(context))
            return;

        if (context.Node is not InvocationExpressionSyntax invocation)
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol == null)
            return;

        // DbContext.Set<T>() 是业务代码绕过 repository/query service 的主要入口。
        if (symbol.Name == "Set" && InheritsFromOrEquals(symbol.ContainingType, efDbContext))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ForbiddenDbSetRule,
                invocation.GetLocation(),
                symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
            return;
        }

        // EF raw SQL 扩展方法通过方法名前缀和 EF 命名空间识别。
        // 原生 SQL 的批准路径是 Atlas executor，由它集中处理租户作用域、参数化和审计行为。
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

    /// <summary>
    /// 判断解析后的类型符号是否跨越了租户数据访问边界。
    /// </summary>
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

    /// <summary>
    /// 当 <paramref name="type"/> 与 <paramref name="baseType"/> 相同，或继承自 <paramref name="baseType"/> 时返回 true。
    /// </summary>
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

    /// <summary>
    /// Runtime namespaces are the only approved place in non-data service assemblies that may touch tenant EF primitives.
    /// </summary>
    private static bool IsApprovedRuntimeLocation(SyntaxNodeAnalysisContext context)
    {
        var namespaceName = context.ContainingSymbol?.ContainingNamespace?.ToDisplayString();
        return namespaceName != null &&
               namespaceName.StartsWith("Atlas.Services.Tenant.Runtime", StringComparison.Ordinal);
    }

    /// <summary>
    /// 控制哪些程序集需要执行租户边界检查。
    /// </summary>
    /// <remarks>
    /// 数据访问项目、测试项目和部分基础设施项目会被排除，因为它们要么负责实现该边界，
    /// 要么需要在组合服务、测试或后台运行时管道中使用更底层的访问能力。
    /// </remarks>
    private static bool ShouldEnforce(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return false;

        var name = assemblyName!;

        // 忽略第三方程序集，以及任何不遵循 Atlas.* 命名约定的本地项目。
        if (!name.StartsWith("Atlas.", StringComparison.Ordinal))
            return false;

        // 被批准的例外：
        // - Tests 需要构造测试夹具，并有意覆盖底层数据访问行为。
        // - Atlas.Data.* 拥有 DbContext、Repository、EF 配置和 migration 相关代码。
        // - Atlas.Extensions.DependencyInjection 负责组合基础设施服务。
        // - Atlas.BackgroundTasks 是后台执行平面，当前直接管理 global job claim SQL。
        // - Atlas.Services.Tenant.Runtime.* 的细粒度豁免在 IsApprovedRuntimeLocation 中处理。
        if (name.EndsWith(".Tests", StringComparison.Ordinal) ||
            name.StartsWith("Atlas.Data.", StringComparison.Ordinal) ||
            name is "Atlas.Analyzers" or "Atlas.LocalSetup" or
                "Atlas.Extensions.DependencyInjection" or "Atlas.BackgroundTasks")
        {
            return false;
        }

        return true;
    }
}
