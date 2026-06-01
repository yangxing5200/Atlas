namespace Atlas.Core.Authorization;

public interface IAtlasAuthorizationConditionEvaluator
{
    bool IsSatisfied(
        AtlasAuthorizationCondition? condition,
        IReadOnlySet<string> permissions,
        IReadOnlySet<string> capabilities,
        IReadOnlySet<string> featureFlags);
}

public sealed class AtlasAuthorizationConditionEvaluator : IAtlasAuthorizationConditionEvaluator
{
    public bool IsSatisfied(
        AtlasAuthorizationCondition? condition,
        IReadOnlySet<string> permissions,
        IReadOnlySet<string> capabilities,
        IReadOnlySet<string> featureFlags)
    {
        if (condition == null)
            return true;

        permissions ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        capabilities ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        featureFlags ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return Evaluate(condition, permissions, capabilities, featureFlags);
    }

    private static bool Evaluate(
        AtlasAuthorizationCondition condition,
        IReadOnlySet<string> permissions,
        IReadOnlySet<string> capabilities,
        IReadOnlySet<string> featureFlags)
    {
        if (!string.IsNullOrWhiteSpace(condition.Permission) &&
            !permissions.Contains(NormalizeCode(condition.Permission)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(condition.Capability) &&
            !capabilities.Contains(NormalizeCode(condition.Capability)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(condition.FeatureFlag) &&
            !featureFlags.Contains(NormalizeCode(condition.FeatureFlag)))
        {
            return false;
        }

        if (condition.All.Count > 0 &&
            condition.All.Any(item => !Evaluate(item, permissions, capabilities, featureFlags)))
        {
            return false;
        }

        if (condition.Any.Count > 0 &&
            condition.Any.All(item => !Evaluate(item, permissions, capabilities, featureFlags)))
        {
            return false;
        }

        if (condition.Not != null &&
            Evaluate(condition.Not, permissions, capabilities, featureFlags))
        {
            return false;
        }

        return true;
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToLowerInvariant();
    }
}
