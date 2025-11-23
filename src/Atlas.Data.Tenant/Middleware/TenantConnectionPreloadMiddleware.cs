using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Middleware
{
    /// <summary>
    /// Preloads tenant connection strings and data scope into cache during request initialization.
    /// Enables synchronous repository operations by ensuring required data is cached.
    /// </summary>
    public class TenantConnectionPreloadMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantConnectionPreloadMiddleware> _logger;

        public TenantConnectionPreloadMiddleware(
            RequestDelegate next,
            ILogger<TenantConnectionPreloadMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var connProvider = context.RequestServices.GetService<ITenantDbConnProvider>();
            var dataScope = context.RequestServices.GetService<IDataScope>();

            if (connProvider?.TenantId != null)
            {
                try
                {
                    var ct = context.RequestAborted;

                    // Preload connection strings in parallel
                    var masterTask = connProvider.GetConnStringAsync(ct);
                    var readonlyTask = connProvider.GetReadonlyConnStringAsync(ct);

                    await Task.WhenAll(masterTask, readonlyTask);

                    // Preload data scope if store context exists
                    if (dataScope?.StoreId.HasValue == true)
                    {
                        await dataScope.PreloadShareStoreIdsAsync(ct);
                    }

                    _logger.LogDebug(
                        "Preload completed - TenantId: {TenantId}, StoreId: {StoreId}",
                        connProvider.TenantId,
                        dataScope?.StoreId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Preload failed - TenantId: {TenantId}, StoreId: {StoreId}. Request will continue.",
                        connProvider.TenantId,
                        dataScope?.StoreId);
                }
            }

            await _next(context);
        }
    }
}