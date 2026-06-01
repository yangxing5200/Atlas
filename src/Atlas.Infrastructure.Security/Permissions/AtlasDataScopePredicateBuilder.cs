using System.Linq.Expressions;
using System.Reflection;
using Atlas.Core.Authorization;

namespace Atlas.Infrastructure.Security.Permissions;

public sealed class AtlasDataScopePredicateBuilder : IAtlasDataScopePredicateBuilder
{
    private readonly IAtlasAuthorizationCatalog _authorizationCatalog;

    public AtlasDataScopePredicateBuilder(IAtlasAuthorizationCatalog authorizationCatalog)
    {
        _authorizationCatalog = authorizationCatalog ?? throw new ArgumentNullException(nameof(authorizationCatalog));
    }

    public Expression<Func<TResource, bool>> BuildPredicate<TResource>(
        AtlasDataAccessContext context)
        where TResource : class
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_authorizationCatalog.DataResources.TryGetValue(context.ResourceCode, out var dataResource))
            return False<TResource>();

        if (!dataResource.SupportedScopes.Contains(context.ScopeType))
            return False<TResource>();

        var parameter = Expression.Parameter(typeof(TResource), "entity");
        var tenant = EqualToLong(parameter, dataResource.TenantField, context.TenantId);
        if (tenant == null)
            return False<TResource>();

        var scope = BuildScopeExpression<TResource>(parameter, dataResource, context);
        if (scope == null)
            return False<TResource>();

        return Expression.Lambda<Func<TResource, bool>>(
            Expression.AndAlso(tenant, scope),
            parameter);
    }

    private static Expression? BuildScopeExpression<TResource>(
        ParameterExpression parameter,
        AtlasDataResourceDefinition dataResource,
        AtlasDataAccessContext context)
    {
        return context.ScopeType switch
        {
            AtlasDataScopeType.AllTenant => Expression.Constant(true),
            AtlasDataScopeType.CurrentStore => string.IsNullOrWhiteSpace(dataResource.StoreField) || !context.StoreId.HasValue
                ? null
                : EqualToLong(parameter, dataResource.StoreField, context.StoreId.Value),
            AtlasDataScopeType.SharedStores => string.IsNullOrWhiteSpace(dataResource.StoreField)
                ? null
                : ContainsLong(parameter, dataResource.StoreField, context.SharedStoreIds),
            AtlasDataScopeType.AssignedStores => string.IsNullOrWhiteSpace(dataResource.StoreField)
                ? null
                : ContainsLong(parameter, dataResource.StoreField, context.AssignedStoreIds),
            AtlasDataScopeType.Own => string.IsNullOrWhiteSpace(dataResource.OwnerField) || context.UserId <= 0
                ? null
                : EqualToLong(parameter, dataResource.OwnerField, context.UserId),
            _ => null
        };
    }

    private static Expression? EqualToLong(
        ParameterExpression parameter,
        string propertyName,
        long expectedValue)
    {
        var property = ResolveProperty(parameter.Type, propertyName);
        if (property == null)
            return null;

        var propertyExpression = Expression.Property(parameter, property);
        return propertyExpression.Type switch
        {
            var type when type == typeof(long) => Expression.Equal(
                propertyExpression,
                Expression.Constant(expectedValue)),
            var type when type == typeof(long?) => Expression.Equal(
                propertyExpression,
                Expression.Constant(expectedValue, typeof(long?))),
            var type when type == typeof(int) => Expression.Equal(
                propertyExpression,
                Expression.Constant(checked((int)expectedValue))),
            var type when type == typeof(int?) => Expression.Equal(
                propertyExpression,
                Expression.Constant(checked((int)expectedValue), typeof(int?))),
            _ => null
        };
    }

    private static Expression? ContainsLong(
        ParameterExpression parameter,
        string propertyName,
        IReadOnlyCollection<long> values)
    {
        if (values.Count == 0)
            return Expression.Constant(false);

        var property = ResolveProperty(parameter.Type, propertyName);
        if (property == null)
            return null;

        var normalizedValues = values.Distinct().ToArray();
        var propertyExpression = Expression.Property(parameter, property);
        if (propertyExpression.Type == typeof(long))
        {
            return Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                new[] { typeof(long) },
                Expression.Constant(normalizedValues),
                propertyExpression);
        }

        if (propertyExpression.Type == typeof(long?))
        {
            var hasValue = Expression.Property(propertyExpression, nameof(Nullable<long>.HasValue));
            var value = Expression.Property(propertyExpression, nameof(Nullable<long>.Value));
            var contains = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                new[] { typeof(long) },
                Expression.Constant(normalizedValues),
                value);
            return Expression.AndAlso(hasValue, contains);
        }

        if (propertyExpression.Type == typeof(int))
        {
            var intValues = normalizedValues.Select(x => checked((int)x)).ToArray();
            return Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                new[] { typeof(int) },
                Expression.Constant(intValues),
                propertyExpression);
        }

        if (propertyExpression.Type == typeof(int?))
        {
            var intValues = normalizedValues.Select(x => checked((int)x)).ToArray();
            var hasValue = Expression.Property(propertyExpression, nameof(Nullable<int>.HasValue));
            var value = Expression.Property(propertyExpression, nameof(Nullable<int>.Value));
            var contains = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                new[] { typeof(int) },
                Expression.Constant(intValues),
                value);
            return Expression.AndAlso(hasValue, contains);
        }

        return null;
    }

    private static PropertyInfo? ResolveProperty(Type resourceType, string propertyName)
    {
        return resourceType.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
    }

    private static Expression<Func<TResource, bool>> False<TResource>()
        where TResource : class
    {
        return _ => false;
    }
}
