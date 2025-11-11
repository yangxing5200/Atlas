// Scoping/ScopeContextAccessor.cs
using System.Threading;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Scoping
{
    public class ScopeContextAccessor : IScopeContextAccessor
    {
        private static readonly AsyncLocal<ScopeContext?> _current = new();

        public ScopeContext? Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }

        public string? TenantId => Current?.TenantId;
        public string? StoreId => Current?.StoreId;
        public string? UserId => Current?.UserId;
        public bool HasTenant => !string.IsNullOrEmpty(TenantId);
        public bool HasStore => !string.IsNullOrEmpty(StoreId);
        public bool HasUser => !string.IsNullOrEmpty(UserId);
    }
}