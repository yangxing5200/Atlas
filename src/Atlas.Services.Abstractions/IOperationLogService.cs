using Atlas.Models.Requests;

namespace Atlas.Services.Abstractions
{
    public interface IOperationLogService
    {
        /// <summary>
        /// 记录操作日志
        /// </summary>
        Task LogOperationAsync(LogOperationRequest request);
    }
}
