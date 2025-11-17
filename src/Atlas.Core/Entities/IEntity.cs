namespace Atlas.Core.Entities
{
    /// <summary>
    /// 雪花ID标记接口
    /// 实现此接口的实体将使用雪花算法自动生成ID
    /// 未实现此接口的实体将使用数据库自增ID
    /// </summary>
    public interface ISnowflakeId
    {
        long Id { get; set; }
    }
    public interface IBaseEntity<T>
    {
        T Id { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }
    public interface IBaseEntity : IBaseEntity<long>
    {
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

    /// <summary>
    /// 共享数据标记接口
    /// 实现此接口的实体，数据在门店共享范围内可见
    /// 共享规则：
    /// - 总部 ↔ 下级直营门店（双向共享）
    /// - 直营门店 ↔ 直营门店（同级共享）
    /// - 加盟门店：独享
    /// </summary>
    public interface ISharedEntity : ITenantEntity, IStoreEntity
    {
    }

    /// <summary>
    /// 门店独享数据标记接口
    /// 实现此接口的实体，数据仅当前门店可见
    /// </summary>
    public interface IStoreOnlyEntity : ITenantEntity, IStoreEntity
    {
    }
}