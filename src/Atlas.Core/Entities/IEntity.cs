namespace Atlas.Core.Entities
{
    public interface IBaseEntity
    {
        long Id { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }

    public interface IAuditable
    {
        long? CreatedBy { get; set; }
        long? UpdatedBy { get; set; }
    }

    public interface ISoftDelete
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
        long? DeletedBy { get; set; }
    }

    public interface IVersioned
    {
        int Version { get; set; }
    }

    public interface ITenantEntity
    {
        long TenantId { get; set; }
    }

    public interface IStoreEntity
    {
        long StoreId { get; set; }
    }
}