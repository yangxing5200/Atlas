using Atlas.Core.Services;
using Atlas.Data.Common.Interceptors;
using Atlas.Data.Tenant.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Context
{
    /// <summary>
    /// Factory for creating tenant-scoped DbContext instances with connection string resolution.
    /// Implements instance caching within DI scope to ensure consistency.
    /// </summary>
    /// <remarks>
    /// THREAD SAFETY:
    /// - Registered as Scoped in DI container (one instance per request)
    /// - Internal caching is thread-safe using SemaphoreSlim
    /// - DbContext instances are NOT thread-safe; ensure single-threaded access
    /// </remarks>
    public class TenantDbContextFactory : ITenantDbContextFactory, IAsyncDisposable
    {
        private readonly ITenantDbConnProvider _connProvider;
        private readonly ICurrentIdentity _currentIdentity;
        private readonly AuditInterceptor _auditInterceptor;
        private readonly ILogger<TenantDbContextFactory> _logger;

        private readonly SemaphoreSlim _lock = new(1, 1);
        private AtlasTenantDbContext? _cachedDbContext;
        private AtlasTenantDbContext? _cachedReadonlyDbContext;
        private AtlasTenantDbContext? _cachedReportDbContext;
        
        // Caches for explicit tenantId DbContext instances (used in login scenarios)
        private readonly ConcurrentDictionary<long, AtlasTenantDbContext> _explicitTenantContexts = new();
        private readonly ConcurrentDictionary<long, AtlasTenantDbContext> _explicitReadonlyTenantContexts = new();
        private readonly ConcurrentDictionary<long, AtlasTenantDbContext> _explicitReportTenantContexts = new();
        
        private bool _disposed;

        public TenantDbContextFactory(
            ITenantDbConnProvider connProvider,
            ICurrentIdentity currentIdentity,
            AuditInterceptor auditInterceptor,
            ILogger<TenantDbContextFactory> logger)
        {
            _connProvider = connProvider;
            _currentIdentity = currentIdentity;
            _auditInterceptor = auditInterceptor;
            _logger = logger;
        }

        /// <summary>
        /// Gets or creates master database context (read-write).
        /// Returns cached instance within same scope.
        /// </summary>
        public async Task<AtlasTenantDbContext> GetDbContextAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (_cachedDbContext != null)
                return _cachedDbContext;

            await _lock.WaitAsync(ct);
            try
            {
                if (_cachedDbContext != null)
                    return _cachedDbContext;

                var connString = await _connProvider.GetConnStringAsync(ct);
                _cachedDbContext = CreateDbContext(connString, isReadonly: false);

                _logger.LogDebug("Created master DbContext for TenantId: {TenantId}", _currentIdentity.TenantId);
                return _cachedDbContext;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets or creates readonly database context.
        /// Returns cached instance within same scope.
        /// </summary>
        public async Task<AtlasTenantDbContext> GetReadonlyDbContextAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (_cachedReadonlyDbContext != null)
                return _cachedReadonlyDbContext;

            await _lock.WaitAsync(ct);
            try
            {
                if (_cachedReadonlyDbContext != null)
                    return _cachedReadonlyDbContext;

                var connString = await _connProvider.GetReadonlyConnStringAsync(ct);
                _cachedReadonlyDbContext = CreateDbContext(connString, isReadonly: true);

                _logger.LogDebug("Created readonly DbContext for TenantId: {TenantId}", _currentIdentity.TenantId);
                return _cachedReadonlyDbContext;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets or creates report database context.
        /// Returns cached instance within same scope.
        /// </summary>
        public async Task<AtlasTenantDbContext> GetReportDbContextAsync(CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (_cachedReportDbContext != null)
                return _cachedReportDbContext;

            await _lock.WaitAsync(ct);
            try
            {
                if (_cachedReportDbContext != null)
                    return _cachedReportDbContext;

                var connString = await _connProvider.GetReportConnStringAsync(ct);
                _cachedReportDbContext = CreateDbContext(connString, isReadonly: true);

                _logger.LogDebug("Created report DbContext for TenantId: {TenantId}", _currentIdentity.TenantId);
                return _cachedReportDbContext;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets or creates master database context with explicit tenantId.
        /// Returns cached instance within same scope for the same tenantId.
        /// </summary>
        /// <param name="tenantId">The tenant identifier</param>
        /// <param name="ct">Cancellation token</param>
        public async Task<AtlasTenantDbContext> GetDbContextAsync(long tenantId, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            
            if (_explicitTenantContexts.TryGetValue(tenantId, out var cached))
                return cached;
                
            await _lock.WaitAsync(ct);
            try
            {
                if (_explicitTenantContexts.TryGetValue(tenantId, out cached))
                    return cached;
                    
                var connString = await _connProvider.GetConnStringAsync(tenantId, ct);
                var context = CreateDbContext(connString, isReadonly: false);
                _explicitTenantContexts.TryAdd(tenantId, context);
                
                _logger.LogDebug("Created master DbContext for explicit TenantId: {TenantId}", tenantId);
                return context;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets or creates readonly database context with explicit tenantId.
        /// Returns cached instance within same scope for the same tenantId.
        /// </summary>
        /// <param name="tenantId">The tenant identifier</param>
        /// <param name="ct">Cancellation token</param>
        public async Task<AtlasTenantDbContext> GetReadonlyDbContextAsync(long tenantId, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            
            if (_explicitReadonlyTenantContexts.TryGetValue(tenantId, out var cached))
                return cached;
                
            await _lock.WaitAsync(ct);
            try
            {
                if (_explicitReadonlyTenantContexts.TryGetValue(tenantId, out cached))
                    return cached;
                    
                var connString = await _connProvider.GetReadonlyConnStringAsync(tenantId, ct);
                var context = CreateDbContext(connString, isReadonly: true);
                _explicitReadonlyTenantContexts.TryAdd(tenantId, context);
                
                _logger.LogDebug("Created readonly DbContext for explicit TenantId: {TenantId}", tenantId);
                return context;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets or creates report database context with explicit tenantId.
        /// Returns cached instance within same scope for the same tenantId.
        /// </summary>
        /// <param name="tenantId">The tenant identifier</param>
        /// <param name="ct">Cancellation token</param>
        public async Task<AtlasTenantDbContext> GetReportDbContextAsync(long tenantId, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            
            if (_explicitReportTenantContexts.TryGetValue(tenantId, out var cached))
                return cached;
                
            await _lock.WaitAsync(ct);
            try
            {
                if (_explicitReportTenantContexts.TryGetValue(tenantId, out cached))
                    return cached;
                    
                var connString = await _connProvider.GetReportConnStringAsync(tenantId, ct);
                var context = CreateDbContext(connString, isReadonly: true);
                _explicitReportTenantContexts.TryAdd(tenantId, context);
                
                _logger.LogDebug("Created report DbContext for explicit TenantId: {TenantId}", tenantId);
                return context;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Creates new DbContext instance with specified connection string.
        /// </summary>
        private AtlasTenantDbContext CreateDbContext(string connectionString, bool isReadonly)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AtlasTenantDbContext>();

            optionsBuilder.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                mysqlOptions =>
                {
                    mysqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });

            if (isReadonly)
            {
                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }
            else
            {
                optionsBuilder.AddInterceptors(_auditInterceptor);
            }

            return new AtlasTenantDbContext(optionsBuilder.Options);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TenantDbContextFactory));
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            if (_cachedDbContext != null)
            {
                await _cachedDbContext.DisposeAsync();
                _cachedDbContext = null;
            }

            if (_cachedReadonlyDbContext != null)
            {
                await _cachedReadonlyDbContext.DisposeAsync();
                _cachedReadonlyDbContext = null;
            }

            if (_cachedReportDbContext != null)
            {
                await _cachedReportDbContext.DisposeAsync();
                _cachedReportDbContext = null;
            }
            
            // Dispose all explicit tenantId DbContext instances
            foreach (var context in _explicitTenantContexts.Values)
            {
                await context.DisposeAsync();
            }
            _explicitTenantContexts.Clear();
            
            foreach (var context in _explicitReadonlyTenantContexts.Values)
            {
                await context.DisposeAsync();
            }
            _explicitReadonlyTenantContexts.Clear();
            
            foreach (var context in _explicitReportTenantContexts.Values)
            {
                await context.DisposeAsync();
            }
            _explicitReportTenantContexts.Clear();

            _lock.Dispose();
            _disposed = true;
        }
    }
}