using Atlas.Core.Enums;

namespace Atlas.Core.Authorization;

public enum AtlasPackageType
{
    Edition = 0,
    Addon = 1,
    Trial = 2,
    Internal = 3
}

public enum AtlasPermissionRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum AtlasDataScopeType
{
    AllTenant = 0,
    CurrentStore = 1,
    SharedStores = 2,
    AssignedStores = 3,
    Department = 4,
    Own = 5,
    Custom = 6
}

public enum AtlasEntitlementSubjectType
{
    Tenant = 0,
    Store = 1,
    Department = 2
}

public enum AtlasEntitlementSource
{
    Order = 0,
    Manual = 1,
    Trial = 2,
    Inherited = 3,
    System = 4
}

public enum AtlasEntitlementStatus
{
    Active = 0,
    Paused = 1,
    Cancelled = 2,
    Expired = 3
}

public interface IAtlasAuthorizationCatalog
{
    IReadOnlyDictionary<string, AtlasCapabilityDefinition> Capabilities { get; }

    IReadOnlyDictionary<string, AtlasPermissionDefinition> Permissions { get; }

    IReadOnlyDictionary<string, AtlasPackageDefinition> Packages { get; }

    IReadOnlyCollection<AtlasPackageCapabilityDefinition> PackageCapabilities { get; }

    IReadOnlyDictionary<string, AtlasMenuItemDefinition> MenuItems { get; }

    IReadOnlyDictionary<string, AtlasDataResourceDefinition> DataResources { get; }
}

public sealed record AtlasCapabilityDefinition(
    string Code,
    string Name,
    string Category,
    string? Description,
    bool IsEnabled,
    string SourceModule);

public sealed record AtlasPermissionDefinition(
    string Code,
    string Name,
    string CapabilityCode,
    string Module,
    PermissionScope Scope,
    string Resource,
    string Action,
    bool IsAssignable,
    bool IsSystem,
    AtlasPermissionRiskLevel RiskLevel,
    string? Description,
    bool IsEnabled,
    string SourceModule);

public sealed record AtlasPackageDefinition(
    string Code,
    string Name,
    AtlasPackageType Type,
    string? Description,
    bool IsEnabled,
    string SourceModule);

public sealed record AtlasPackageCapabilityDefinition(
    string PackageCode,
    string CapabilityCode,
    string? LimitJson,
    string? OptionJson,
    string SourceModule);

public sealed record AtlasMenuItemDefinition(
    string Code,
    string Name,
    string Route,
    string? ParentCode,
    string? Icon,
    int SortOrder,
    AtlasAuthorizationCondition? VisibleWhen,
    bool IsEnabled,
    string SourceModule);

public sealed record AtlasDataResourceDefinition(
    string Code,
    string Name,
    string? EntityType,
    string TenantField,
    string? StoreField,
    string? OwnerField,
    IReadOnlyCollection<AtlasDataScopeType> SupportedScopes,
    string SourceModule);

public sealed record AtlasAuthorizationCondition
{
    public string? Permission { get; init; }

    public string? Capability { get; init; }

    public string? FeatureFlag { get; init; }

    public IReadOnlyList<AtlasAuthorizationCondition> All { get; init; } = Array.Empty<AtlasAuthorizationCondition>();

    public IReadOnlyList<AtlasAuthorizationCondition> Any { get; init; } = Array.Empty<AtlasAuthorizationCondition>();

    public AtlasAuthorizationCondition? Not { get; init; }

    public static AtlasAuthorizationCondition RequirePermission(string code)
    {
        return new AtlasAuthorizationCondition { Permission = RequireCode(code, nameof(code)) };
    }

    public static AtlasAuthorizationCondition RequireCapability(string code)
    {
        return new AtlasAuthorizationCondition { Capability = RequireCode(code, nameof(code)) };
    }

    public static AtlasAuthorizationCondition RequireFeatureFlag(string code)
    {
        return new AtlasAuthorizationCondition { FeatureFlag = RequireCode(code, nameof(code)) };
    }

    public static AtlasAuthorizationCondition AllOf(params AtlasAuthorizationCondition[] conditions)
    {
        return new AtlasAuthorizationCondition { All = RequireConditions(conditions, nameof(conditions)) };
    }

    public static AtlasAuthorizationCondition AnyOf(params AtlasAuthorizationCondition[] conditions)
    {
        return new AtlasAuthorizationCondition { Any = RequireConditions(conditions, nameof(conditions)) };
    }

    public static AtlasAuthorizationCondition NotOf(AtlasAuthorizationCondition condition)
    {
        return new AtlasAuthorizationCondition
        {
            Not = condition ?? throw new ArgumentNullException(nameof(condition))
        };
    }

    internal IEnumerable<string> EnumeratePermissionReferences()
    {
        if (!string.IsNullOrWhiteSpace(Permission))
            yield return NormalizeCode(Permission);

        foreach (var condition in All)
        {
            foreach (var code in condition.EnumeratePermissionReferences())
                yield return code;
        }

        foreach (var condition in Any)
        {
            foreach (var code in condition.EnumeratePermissionReferences())
                yield return code;
        }

        if (Not != null)
        {
            foreach (var code in Not.EnumeratePermissionReferences())
                yield return code;
        }
    }

    internal IEnumerable<string> EnumerateCapabilityReferences()
    {
        if (!string.IsNullOrWhiteSpace(Capability))
            yield return NormalizeCode(Capability);

        foreach (var condition in All)
        {
            foreach (var code in condition.EnumerateCapabilityReferences())
                yield return code;
        }

        foreach (var condition in Any)
        {
            foreach (var code in condition.EnumerateCapabilityReferences())
                yield return code;
        }

        if (Not != null)
        {
            foreach (var code in Not.EnumerateCapabilityReferences())
                yield return code;
        }
    }

    private static IReadOnlyList<AtlasAuthorizationCondition> RequireConditions(
        IReadOnlyList<AtlasAuthorizationCondition>? conditions,
        string parameterName)
    {
        if (conditions == null || conditions.Count == 0)
            throw new ArgumentException("At least one condition is required.", parameterName);

        return conditions;
    }

    internal static string NormalizeCode(string code)
    {
        return RequireCode(code, nameof(code)).ToLowerInvariant();
    }

    private static string RequireCode(string code, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", parameterName);

        return code.Trim();
    }
}

public sealed class AtlasAuthorizationCatalog : IAtlasAuthorizationCatalog
{
    public AtlasAuthorizationCatalog(
        IReadOnlyDictionary<string, AtlasCapabilityDefinition> capabilities,
        IReadOnlyDictionary<string, AtlasPermissionDefinition> permissions,
        IReadOnlyDictionary<string, AtlasPackageDefinition> packages,
        IReadOnlyCollection<AtlasPackageCapabilityDefinition> packageCapabilities,
        IReadOnlyDictionary<string, AtlasMenuItemDefinition> menuItems,
        IReadOnlyDictionary<string, AtlasDataResourceDefinition> dataResources)
    {
        Capabilities = capabilities;
        Permissions = permissions;
        Packages = packages;
        PackageCapabilities = packageCapabilities;
        MenuItems = menuItems;
        DataResources = dataResources;
    }

    public static AtlasAuthorizationCatalog Empty { get; } = new(
        new Dictionary<string, AtlasCapabilityDefinition>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, AtlasPermissionDefinition>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, AtlasPackageDefinition>(StringComparer.OrdinalIgnoreCase),
        Array.Empty<AtlasPackageCapabilityDefinition>(),
        new Dictionary<string, AtlasMenuItemDefinition>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, AtlasDataResourceDefinition>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyDictionary<string, AtlasCapabilityDefinition> Capabilities { get; }

    public IReadOnlyDictionary<string, AtlasPermissionDefinition> Permissions { get; }

    public IReadOnlyDictionary<string, AtlasPackageDefinition> Packages { get; }

    public IReadOnlyCollection<AtlasPackageCapabilityDefinition> PackageCapabilities { get; }

    public IReadOnlyDictionary<string, AtlasMenuItemDefinition> MenuItems { get; }

    public IReadOnlyDictionary<string, AtlasDataResourceDefinition> DataResources { get; }
}

public sealed class AtlasAuthorizationCatalogBuilder
{
    private readonly Dictionary<string, AtlasCapabilityDefinition> _capabilities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AtlasPermissionDefinition> _permissions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AtlasPackageDefinition> _packages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AtlasPackageCapabilityDefinition> _packageCapabilities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AtlasMenuItemDefinition> _menuItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AtlasDataResourceDefinition> _dataResources = new(StringComparer.OrdinalIgnoreCase);

    public AtlasAuthorizationCatalogBuilder(string sourceModule = "Unknown")
    {
        SourceModule = string.IsNullOrWhiteSpace(sourceModule)
            ? "Unknown"
            : sourceModule.Trim();
    }

    public string SourceModule { get; }

    public AtlasAuthorizationCatalogBuilder AddCapability(
        string code,
        string name,
        string category = "",
        string? description = null,
        bool isEnabled = true)
    {
        var normalizedCode = NormalizeCode(code);
        AddUnique(
            _capabilities,
            normalizedCode,
            new AtlasCapabilityDefinition(
                normalizedCode,
                RequireText(name, nameof(name)),
                category?.Trim() ?? string.Empty,
                description?.Trim(),
                isEnabled,
                SourceModule),
            "capability");

        return this;
    }

    public AtlasAuthorizationCatalogBuilder AddPermission(
        string code,
        string name,
        string capabilityCode,
        string module,
        PermissionScope scope = PermissionScope.Tenant,
        string resource = "",
        string action = "",
        bool isAssignable = true,
        bool isSystem = false,
        AtlasPermissionRiskLevel riskLevel = AtlasPermissionRiskLevel.Low,
        string? description = null,
        bool isEnabled = true)
    {
        var normalizedCode = NormalizeCode(code);
        var normalizedCapabilityCode = NormalizeCode(capabilityCode);
        AddUnique(
            _permissions,
            normalizedCode,
            new AtlasPermissionDefinition(
                normalizedCode,
                RequireText(name, nameof(name)),
                normalizedCapabilityCode,
                RequireText(module, nameof(module)),
                scope,
                resource?.Trim() ?? string.Empty,
                action?.Trim() ?? string.Empty,
                isAssignable,
                isSystem,
                riskLevel,
                description?.Trim(),
                isEnabled,
                SourceModule),
            "permission");

        return this;
    }

    public AtlasAuthorizationCatalogBuilder AddPackage(
        string code,
        string name,
        AtlasPackageType type,
        string? description = null,
        bool isEnabled = true)
    {
        var normalizedCode = NormalizeCode(code);
        var package = new AtlasPackageDefinition(
            normalizedCode,
            RequireText(name, nameof(name)),
            type,
            description?.Trim(),
            isEnabled,
            SourceModule);

        if (_packages.TryGetValue(normalizedCode, out var existing))
        {
            if (!PackageMatches(existing, package))
            {
                throw new InvalidOperationException(
                    $"Package '{normalizedCode}' is declared inconsistently by '{existing.SourceModule}' and '{SourceModule}'.");
            }

            return this;
        }

        _packages.Add(normalizedCode, package);
        return this;
    }

    public AtlasAuthorizationCatalogBuilder AddPackageCapability(
        string packageCode,
        string capabilityCode,
        string? limitJson = null,
        string? optionJson = null)
    {
        var normalizedPackageCode = NormalizeCode(packageCode);
        var normalizedCapabilityCode = NormalizeCode(capabilityCode);
        var key = $"{normalizedPackageCode}:{normalizedCapabilityCode}";
        var item = new AtlasPackageCapabilityDefinition(
            normalizedPackageCode,
            normalizedCapabilityCode,
            limitJson?.Trim(),
            optionJson?.Trim(),
            SourceModule);

        if (_packageCapabilities.TryGetValue(key, out var existing))
        {
            if (!string.Equals(existing.LimitJson, item.LimitJson, StringComparison.Ordinal) ||
                !string.Equals(existing.OptionJson, item.OptionJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Package capability '{key}' is declared inconsistently by '{existing.SourceModule}' and '{SourceModule}'.");
            }

            return this;
        }

        _packageCapabilities.Add(key, item);
        return this;
    }

    public AtlasAuthorizationCatalogBuilder AddMenuItem(
        string code,
        string name,
        string route,
        string? parentCode = null,
        string? icon = null,
        int sortOrder = 0,
        AtlasAuthorizationCondition? visibleWhen = null,
        bool isEnabled = true)
    {
        var normalizedCode = NormalizeCode(code);
        AddUnique(
            _menuItems,
            normalizedCode,
            new AtlasMenuItemDefinition(
                normalizedCode,
                RequireText(name, nameof(name)),
                RequireText(route, nameof(route)),
                string.IsNullOrWhiteSpace(parentCode) ? null : NormalizeCode(parentCode),
                icon?.Trim(),
                sortOrder,
                visibleWhen,
                isEnabled,
                SourceModule),
            "menu item");

        return this;
    }

    public AtlasAuthorizationCatalogBuilder AddDataResource(
        string code,
        string name,
        string? entityType = null,
        string tenantField = "TenantId",
        string? storeField = null,
        string? ownerField = null,
        IReadOnlyCollection<AtlasDataScopeType>? supportedScopes = null)
    {
        var normalizedCode = NormalizeCode(code);
        AddUnique(
            _dataResources,
            normalizedCode,
            new AtlasDataResourceDefinition(
                normalizedCode,
                RequireText(name, nameof(name)),
                entityType?.Trim(),
                RequireText(tenantField, nameof(tenantField)),
                string.IsNullOrWhiteSpace(storeField) ? null : storeField.Trim(),
                string.IsNullOrWhiteSpace(ownerField) ? null : ownerField.Trim(),
                supportedScopes is { Count: > 0 }
                    ? supportedScopes.Distinct().ToArray()
                    : new[] { AtlasDataScopeType.CurrentStore },
                SourceModule),
            "data resource");

        return this;
    }

    public AtlasAuthorizationCatalogBuilder Merge(AtlasAuthorizationCatalog catalog)
    {
        foreach (var item in catalog.Capabilities.Values)
            AddUnique(_capabilities, item.Code, item, "capability");

        foreach (var item in catalog.Permissions.Values)
            AddUnique(_permissions, item.Code, item, "permission");

        foreach (var item in catalog.Packages.Values)
        {
            if (_packages.TryGetValue(item.Code, out var existing) && !PackageMatches(existing, item))
                throw new InvalidOperationException($"Package '{item.Code}' is declared inconsistently.");

            _packages[item.Code] = item;
        }

        foreach (var item in catalog.PackageCapabilities)
        {
            var key = $"{item.PackageCode}:{item.CapabilityCode}";
            if (_packageCapabilities.TryGetValue(key, out var existing) &&
                (!string.Equals(existing.LimitJson, item.LimitJson, StringComparison.Ordinal) ||
                 !string.Equals(existing.OptionJson, item.OptionJson, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Package capability '{key}' is declared inconsistently.");
            }

            _packageCapabilities[key] = item;
        }

        foreach (var item in catalog.MenuItems.Values)
            AddUnique(_menuItems, item.Code, item, "menu item");

        foreach (var item in catalog.DataResources.Values)
            AddUnique(_dataResources, item.Code, item, "data resource");

        return this;
    }

    public AtlasAuthorizationCatalog Build(bool validateReferences = true)
    {
        if (validateReferences)
            ValidateReferences();

        return new AtlasAuthorizationCatalog(
            new Dictionary<string, AtlasCapabilityDefinition>(_capabilities, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AtlasPermissionDefinition>(_permissions, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AtlasPackageDefinition>(_packages, StringComparer.OrdinalIgnoreCase),
            _packageCapabilities.Values.ToArray(),
            new Dictionary<string, AtlasMenuItemDefinition>(_menuItems, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AtlasDataResourceDefinition>(_dataResources, StringComparer.OrdinalIgnoreCase));
    }

    private void ValidateReferences()
    {
        foreach (var permission in _permissions.Values)
        {
            if (!_capabilities.ContainsKey(permission.CapabilityCode))
            {
                throw new InvalidOperationException(
                    $"Permission '{permission.Code}' references missing capability '{permission.CapabilityCode}'.");
            }
        }

        foreach (var item in _packageCapabilities.Values)
        {
            if (!_packages.ContainsKey(item.PackageCode))
                throw new InvalidOperationException($"Package capability references missing package '{item.PackageCode}'.");

            if (!_capabilities.ContainsKey(item.CapabilityCode))
                throw new InvalidOperationException($"Package '{item.PackageCode}' references missing capability '{item.CapabilityCode}'.");
        }

        foreach (var item in _menuItems.Values)
        {
            if (item.ParentCode != null && !_menuItems.ContainsKey(item.ParentCode))
                throw new InvalidOperationException($"Menu item '{item.Code}' references missing parent menu '{item.ParentCode}'.");

            if (item.VisibleWhen == null)
                continue;

            foreach (var permissionCode in item.VisibleWhen.EnumeratePermissionReferences())
            {
                if (!_permissions.ContainsKey(permissionCode))
                    throw new InvalidOperationException($"Menu item '{item.Code}' references missing permission '{permissionCode}'.");
            }

            foreach (var capabilityCode in item.VisibleWhen.EnumerateCapabilityReferences())
            {
                if (!_capabilities.ContainsKey(capabilityCode))
                    throw new InvalidOperationException($"Menu item '{item.Code}' references missing capability '{capabilityCode}'.");
            }
        }
    }

    private static void AddUnique<T>(
        IDictionary<string, T> target,
        string code,
        T value,
        string typeName)
        where T : notnull
    {
        if (target.ContainsKey(code))
            throw new InvalidOperationException($"Duplicate {typeName} code '{code}'.");

        target.Add(code, value);
    }

    private static bool PackageMatches(AtlasPackageDefinition left, AtlasPackageDefinition right)
    {
        return string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
               left.Type == right.Type &&
               string.Equals(left.Description, right.Description, StringComparison.Ordinal) &&
               left.IsEnabled == right.IsEnabled;
    }

    private static string NormalizeCode(string code)
    {
        return AtlasAuthorizationCondition.NormalizeCode(code);
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);

        return value.Trim();
    }
}
