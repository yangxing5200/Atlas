namespace Atlas.Infrastructure.Caching.Dependencies;

/// <summary>
/// 依赖级别
/// </summary>
public enum DependencyLevel
{
    /// <summary>
    /// 类型级依赖：整个实体类型变化影响缓存
    /// </summary>
    Type,

    /// <summary>
    /// 实例级依赖：特定实体实例变化影响对应缓存
    /// </summary>
    Instance
}