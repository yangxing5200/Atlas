// Builders/CacheOptionsBuilder.cs
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Data.Tests.Builders
{
    public class CacheOptionsBuilder
    {
        private TimeSpan? _absoluteExpiration;
        private TimeSpan? _slidingExpiration;
        private HashSet<string> _tags = new();
        private CacheScope _scope = CacheScope.Global;

        public static CacheOptionsBuilder Create() => new();

        public CacheOptionsBuilder WithAbsoluteExpiration(TimeSpan expiration)
        {
            _absoluteExpiration = expiration;
            return this;
        }

        public CacheOptionsBuilder WithSlidingExpiration(TimeSpan expiration)
        {
            _slidingExpiration = expiration;
            return this;
        }

        public CacheOptionsBuilder WithTags(params string[] tags)
        {
            _tags = new HashSet<string>(tags);
            return this;
        }

        public CacheOptionsBuilder WithScope(CacheScope scope)
        {
            _scope = scope;
            return this;
        }

        public CacheOptions Build()
        {
            return new CacheOptions
            {
                AbsoluteExpiration = _absoluteExpiration,
                SlidingExpiration = _slidingExpiration,
                Tags = _tags,
                Scope = _scope
            };
        }
    }
}