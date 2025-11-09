using System.Linq.Expressions;

namespace Atlas.Infrastructure.Caching.Dependencies;

/// <summary>
/// 缓存依赖配置
/// </summary>
public class CacheDependency
{
    /// <summary>
    /// 依赖的实体类型
    /// </summary>
    public Type EntityType { get; set; } = null!;

    /// <summary>
    /// 依赖级别
    /// </summary>
    public DependencyLevel Level { get; set; }

    /// <summary>
    /// 实例键选择器（Instance级别需要）
    /// </summary>
    public Func<object, object>? InstanceKeySelector { get; set; }

    /// <summary>
    /// 触发属性表达式列表（空表示任何属性变化都触发）
    /// </summary>
    public List<LambdaExpression> TriggerPropertyExpressions { get; set; } = new();

    // 内部存储
    private readonly List<string> _triggerProperties = new();

    // 公开只读接口
    public IReadOnlyList<string> TriggerProperties => _triggerProperties;

    /// <summary>
    /// 添加触发属性名（内部使用，避免魔法字符串）
    /// </summary>
    internal void AddTriggerProperty(string propertyName)
    {
        if (!string.IsNullOrWhiteSpace(propertyName) && !_triggerProperties.Contains(propertyName))
        {
            _triggerProperties.Add(propertyName);
        }
    }

    /// <summary>
    /// 批量添加触发属性名（内部使用）
    /// </summary>
    internal void AddTriggerProperties(IEnumerable<string> propertyNames)
    {
        foreach (var name in propertyNames)
        {
            AddTriggerProperty(name);
        }
    }
}

/// <summary>
/// 泛型缓存依赖配置（提供类型安全的 API）
/// </summary>
public class CacheDependency<TEntity> : CacheDependency where TEntity : class
{
    public CacheDependency()
    {
        EntityType = typeof(TEntity);
    }

    /// <summary>
    /// 设置实例键选择器
    /// </summary>
    public CacheDependency<TEntity> WithInstanceKey(Expression<Func<TEntity, object>> keySelector)
    {
        Level = DependencyLevel.Instance;
        InstanceKeySelector = entity => keySelector.Compile()((TEntity)entity);
        return this;
    }

    /// <summary>
    /// 添加触发属性
    /// </summary>
    public CacheDependency<TEntity> OnPropertyChange(Expression<Func<TEntity, object>> propertySelector)
    {
        TriggerPropertyExpressions.Add(propertySelector);
        var propertyName = ExtractPropertyName(propertySelector);
        AddTriggerProperty(propertyName);  // 使用内部方法
        return this;
    }

    /// <summary>
    /// 添加多个触发属性
    /// </summary>
    public CacheDependency<TEntity> OnPropertiesChange(params Expression<Func<TEntity, object>>[] propertySelectors)
    {
        foreach (var selector in propertySelectors)
        {
            OnPropertyChange(selector);
        }
        return this;
    }

    /// <summary>
    /// 从表达式提取属性名
    /// </summary>
    private static string ExtractPropertyName(Expression<Func<TEntity, object>> expression)
    {
        if (expression.Body is MemberExpression memberExpr)
        {
            return memberExpr.Member.Name;
        }

        if (expression.Body is UnaryExpression unaryExpr &&
            unaryExpr.Operand is MemberExpression unaryMemberExpr)
        {
            return unaryMemberExpr.Member.Name;
        }

        throw new ArgumentException(
            $"Expression '{expression}' must be a simple property access expression (e.g., x => x.PropertyName)",
            nameof(expression));
    }
}

/// <summary>
/// 缓存依赖构建器（用于流畅的配置 API）
/// </summary>
public static class CacheDependencyBuilder
{
    /// <summary>
    /// 创建类型级依赖
    /// </summary>
    public static CacheDependency<TEntity> OnType<TEntity>() where TEntity : class
    {
        return new CacheDependency<TEntity>
        {
            Level = DependencyLevel.Type
        };
    }

    /// <summary>
    /// 创建实例级依赖
    /// </summary>
    public static CacheDependency<TEntity> OnInstance<TEntity>(
        Expression<Func<TEntity, object>> keySelector) where TEntity : class
    {
        return new CacheDependency<TEntity>().WithInstanceKey(keySelector);
    }
}