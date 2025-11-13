// Core/Models/CacheKeyDefinition.cs
using System;
using System.Collections.Generic;

namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// 缓存键定义（所有缓存必须通过定义访问）
    /// </summary>
    public sealed class CacheKeyDefinition
    {
        /// <summary>
        /// 键名称模板（可包含占位符，如 "product:{id}"）
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 作用域
        /// </summary>
        public CacheScope Scope { get; }

        /// <summary>
        /// 实例键名称（用于占位符替换）
        /// </summary>
        public string? InstanceKeyName { get; }

        /// <summary>
        /// 默认过期时间
        /// </summary>
        public TimeSpan DefaultExpiration { get; }

        /// <summary>
        /// 是否启用 L1 缓存（内存缓存）
        /// </summary>
        public bool EnableL1Cache { get; }

        /// <summary>
        /// 最大随机偏移秒数（防止缓存雪崩）
        /// </summary>
        public int MaxRandomOffsetSeconds { get; }

        /// <summary>
        /// 标签生成器：根据上下文和实例值生成标签
        /// </summary>
        public Func<ScopeContext, object?, IEnumerable<string>>? TagGenerator { get; init; }

        /// <summary>
        /// 描述信息（用于文档和调试）
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// 是否允许为 null（如果为 false，在值为 null 时不缓存）
        /// </summary>
        public bool AllowNull { get; }

        private CacheKeyDefinition(
            string name,
            CacheScope scope,
            string? instanceKeyName,
            TimeSpan defaultExpiration,
            bool enableL1Cache,
            int maxRandomOffsetSeconds,
            string? description,
            bool allowNull)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Key name cannot be empty", nameof(name));

            Name = name;
            Scope = scope;
            InstanceKeyName = instanceKeyName;
            DefaultExpiration = defaultExpiration;
            EnableL1Cache = enableL1Cache;
            MaxRandomOffsetSeconds = maxRandomOffsetSeconds;
            Description = description;
            AllowNull = allowNull;
        }

        /// <summary>
        /// 构建实际的缓存键（不包含 Scope 前缀）
        /// </summary>
        public string BuildKey(object? instanceValue = null)
        {
            if (string.IsNullOrEmpty(InstanceKeyName) || instanceValue == null)
                return Name;

            // 替换占位符
            return Name.Replace($"{{{InstanceKeyName}}}", instanceValue.ToString());
        }

        /// <summary>
        /// 生成缓存选项
        /// </summary>
        public CacheOptions CreateOptions(ScopeContext? context, object? instanceValue = null)
        {
            var expiration = DefaultExpiration;

            // 添加随机偏移（防止缓存雪崩）
            if (MaxRandomOffsetSeconds > 0)
            {
                var random = new Random();
                var offsetSeconds = random.Next(0, MaxRandomOffsetSeconds);
                expiration = expiration.Add(TimeSpan.FromSeconds(offsetSeconds));
            }

            var options = new CacheOptions
            {
                Scope = Scope,
                AbsoluteExpiration = expiration
            };

            // 生成标签
            if (TagGenerator != null && context != null)
            {
                var tags = TagGenerator(context, instanceValue);
                options.Tags = new HashSet<string>(tags);
            }

            return options;
        }

        /// <summary>
        /// 创建缓存键定义的 Builder
        /// </summary>
        public static Builder Create(string name) => new Builder(name);

        /// <summary>
        /// Builder 模式用于创建 CacheKeyDefinition
        /// </summary>
        public class Builder
        {
            private readonly string _name;
            private CacheScope _scope = CacheScope.Tenant;
            private string? _instanceKeyName;
            private TimeSpan _defaultExpiration = TimeSpan.FromHours(1);
            private bool _enableL1Cache = true;
            private int _maxRandomOffsetSeconds = 300;
            private string? _description;
            private bool _allowNull = false;
            private Func<ScopeContext, object?, IEnumerable<string>>? _tagGenerator;

            internal Builder(string name)
            {
                _name = name;
            }

            public Builder WithScope(CacheScope scope)
            {
                _scope = scope;
                return this;
            }

            public Builder WithInstanceKey(string instanceKeyName)
            {
                _instanceKeyName = instanceKeyName;
                return this;
            }

            public Builder WithExpiration(TimeSpan expiration)
            {
                _defaultExpiration = expiration;
                return this;
            }

            public Builder EnableL1Cache(bool enable = true)
            {
                _enableL1Cache = enable;
                return this;
            }

            public Builder WithMaxRandomOffset(int seconds)
            {
                _maxRandomOffsetSeconds = seconds;
                return this;
            }

            public Builder WithDescription(string description)
            {
                _description = description;
                return this;
            }

            public Builder AllowNull(bool allow = true)
            {
                _allowNull = allow;
                return this;
            }

            public Builder WithTagGenerator(Func<ScopeContext, object?, IEnumerable<string>> generator)
            {
                _tagGenerator = generator;
                return this;
            }

            public CacheKeyDefinition Build()
            {
                return new CacheKeyDefinition(
                    _name,
                    _scope,
                    _instanceKeyName,
                    _defaultExpiration,
                    _enableL1Cache,
                    _maxRandomOffsetSeconds,
                    _description,
                    _allowNull)
                {
                    TagGenerator = _tagGenerator
                };
            }
        }
    }
}