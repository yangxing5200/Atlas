using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Atlas.Services
{
    public class OperationLogService : IOperationLogService
    {
        private readonly IRepository<OperationLog> _repository;
        private readonly ILogger<OperationLogService> _logger;

        public OperationLogService(
            IRepository<OperationLog> repository,
            ILogger<OperationLogService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task LogOperationAsync(
            long tenantId,
            long? userId,
            long? storeId,
            string? sessionId,
            string module,
            string operationType,
            string description,
            long? entityId = null,
            string? changes = null,
            string? ipAddress = null,
            bool isSuccess = true,
            string? errorMessage = null)
        {
            try
            {
                var log = new OperationLog
                {
                    TenantId = tenantId,
                    UserId = userId,
                    StoreId = storeId,
                    SessionId = sessionId,
                    Module = module,
                    OperationType = operationType,
                    Description = description,
                    EntityId = entityId,
                    Changes = changes,
                    IpAddress = ipAddress,
                    IsSuccess = isSuccess,
                    ErrorMessage = errorMessage
                };

                await _repository.AddAsync(log, tenantId);
                await _repository.SaveChangesAsync(tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log operation: {Module}/{OperationType}", module, operationType);
            }
        }
    }
}
