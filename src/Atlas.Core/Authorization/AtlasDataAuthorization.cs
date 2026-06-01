using System.Linq.Expressions;

namespace Atlas.Core.Authorization;

public sealed record AtlasDataAccessContext(
    long TenantId,
    long UserId,
    long? StoreId,
    string ResourceCode,
    AtlasDataScopeType ScopeType,
    IReadOnlyCollection<long> SharedStoreIds,
    IReadOnlyCollection<long> AssignedStoreIds,
    long? DepartmentId = null);

public sealed record AtlasDataAccessDecision(
    bool Allowed,
    string Reason)
{
    public static AtlasDataAccessDecision Allow(string reason = "Allowed")
    {
        return new AtlasDataAccessDecision(true, reason);
    }

    public static AtlasDataAccessDecision Deny(string reason)
    {
        return new AtlasDataAccessDecision(false, reason);
    }
}

public interface IAtlasDataScopeContributor<TResource>
{
    ValueTask<AtlasDataAccessDecision> CanAccessAsync(
        TResource resource,
        AtlasDataAccessContext context,
        CancellationToken ct = default);
}

public interface IAtlasDataAccessEvaluator
{
    ValueTask<AtlasDataAccessDecision> CanAccessAsync<TResource>(
        TResource resource,
        AtlasDataAccessContext context,
        CancellationToken ct = default);
}

public interface IAtlasDataScopePredicateBuilder
{
    Expression<Func<TResource, bool>> BuildPredicate<TResource>(
        AtlasDataAccessContext context)
        where TResource : class;
}
