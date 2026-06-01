namespace Atlas.Services.Abstractions.Queries;

/// <summary>
/// Marker for read-model services. QueryServices must compose scoped repository queries instead of opening data contexts directly.
/// </summary>
public interface IQueryService
{
}
