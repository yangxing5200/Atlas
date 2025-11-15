// Scoping/ScopeContextAccessor.cs
using System.Threading;
using Atlas.Core.Services;
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

    public class CurrentUserScopeContextAccessor : IScopeContextAccessor
    {
        private readonly ICurrentIdentity _currentUserService;

        public CurrentUserScopeContextAccessor(ICurrentIdentity currentUserService)
        {
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        }

        public ScopeContext? Current
        {
            get => _currentUserService.IsAuthenticated
                ? new ScopeContext
                {
                    TenantId = _currentUserService.TenantId?.ToString(),
                    StoreId = _currentUserService.StoreId?.ToString(),
                    UserId = _currentUserService.UserId?.ToString(),
                    UserName = _currentUserService.UserName
                }
                : null;
            set => throw new NotSupportedException("Cannot set when using ICurrentUserService");
        }

        public string? TenantId => _currentUserService.TenantId?.ToString();
        public string? StoreId => _currentUserService.StoreId?.ToString();
        public string? UserId => _currentUserService.UserId?.ToString();
        public bool HasTenant => _currentUserService.TenantId.HasValue;
        public bool HasStore => _currentUserService.StoreId.HasValue;
        public bool HasUser => _currentUserService.UserId.HasValue;
    }
}