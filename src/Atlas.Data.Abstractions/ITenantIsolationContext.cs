namespace Atlas.Data.Abstractions
{
    public interface ITenantIsolationContext
    {
        bool TenantFilterEnabled { get; }

        bool TenantWriteGuardEnabled { get; }
    }
}
