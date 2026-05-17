namespace Atlas.Core.Entities.Interfaces
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
    /// <summary>
    /// 所有持久化实体的最小公共契约。
    /// </summary>
    /// <remarks>
    /// CreatedAt/UpdatedAt 由基础设施层统一维护，业务代码通常不应在普通更新流程中覆盖这些字段。
    /// </remarks>
    public interface IBaseEntity<T>
    {
        T Id { get; set; }
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }
    public interface IBaseEntity : IBaseEntity<long>
    {
    }

    /// <summary>
    /// 标记实体需要记录创建人和最后修改人。
    /// </summary>
    public interface IAuditable
    {
        long? CreatedBy { get; set; }
        long? UpdatedBy { get; set; }
    }

    /// <summary>
    /// 标记实体使用软删除，而不是从数据库物理删除。
    /// </summary>
    /// <remarks>
    /// 仓储层会优先设置删除标记，查询层应配合全局过滤或显式条件排除已删除数据。
    /// </remarks>
    public interface ISoftDelete
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
        long? DeletedBy { get; set; }
    }

    /// <summary>
    /// 标记实体支持乐观并发版本号。
    /// </summary>
    public interface IVersioned
    {
        int Version { get; set; }
    }

    /// <summary>
    /// 标记实体属于某个租户，是跨租户隔离的基础字段。
    /// </summary>
    public interface ITenantEntity
    {
        long TenantId { get; set; }
    }

    /// <summary>
    /// 标记实体属于某个门店，用于门店级数据范围过滤。
    /// </summary>
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
