using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Models.Requests;
using Atlas.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Atlas.Services
{
    public class OperationLogService : IOperationLogService
    {
        private readonly IRepository<OperationLog> _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OperationLogService> _logger;

        public OperationLogService(
            IRepository<OperationLog> repository,
            IUnitOfWork unitOfWork,
            ILogger<OperationLogService> logger)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task LogOperationAsync(LogOperationRequest request)
        {
            try
            {
                var log = new OperationLog
                {
                    TenantId = request.TenantId,
                    UserId = request.UserId,
                    StoreId = request.StoreId,
                    SessionId = request.SessionId,
                    Module = request.Module,
                    OperationType = request.OperationType,
                    Description = request.Description,
                    EntityId = request.EntityId,
                    Changes = request.Changes,
                    IpAddress = request.IpAddress,
                    IsSuccess = request.IsSuccess,
                    ErrorMessage = request.ErrorMessage
                };

                await _repository.AddAsync(log, request.TenantId);
                await _unitOfWork.SaveChangesAsync(request.TenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log operation: {Module}/{OperationType}", request.Module, request.OperationType);
            }
        }
    }
}
