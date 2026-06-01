namespace Atlas.Core.DataMasking;

/// <summary>
/// Disables automatic response masking for a controller or action.
/// Use only for explicit reveal endpoints that perform their own permission checks and audit logging.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DisableDataMaskingAttribute : Attribute
{
}
