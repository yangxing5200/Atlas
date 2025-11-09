namespace Atlas.Infrastructure.Caching.Dependencies;

/// <summary>
/// 实体变更信息
/// </summary>
public class EntityChangeInfo
{
    /// <summary>
    /// 实体类型
    /// </summary>
    public Type EntityType { get; set; } = null!;

    /// <summary>
    /// 变更状态
    /// </summary>
    public EntityChangeState State { get; set; }

    /// <summary>
    /// 实体实例
    /// </summary>
    public object Entity { get; set; } = null!;

    /// <summary>
    /// 变更的属性名称列表
    /// </summary>
    public List<string> ModifiedProperties { get; set; } = new();

    /// <summary>
    /// 旧值（修改时）
    /// </summary>
    public Dictionary<string, object?> OldValues { get; set; } = new();

    /// <summary>
    /// 新值
    /// </summary>
    public Dictionary<string, object?> NewValues { get; set; } = new();
}

public enum EntityChangeState
{
    Added,
    Modified,
    Deleted
}