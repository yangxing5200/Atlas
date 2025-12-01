using Atlas.Core.Entities.Tenant;

namespace Atlas.Services.Abstractions
{
    public interface IOperationLogService
    {
        /// <summary>
        /// 记录操作日志
        /// </summary>
        Task LogOperationAsync(
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
            string? errorMessage = null);
    }
}
