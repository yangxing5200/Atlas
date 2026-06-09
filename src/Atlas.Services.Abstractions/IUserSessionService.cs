using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;

namespace Atlas.Services.Abstractions;

public interface IUserSessionService
{
    Task<LoginLogPagedResponse> GetLoginLogsAsync(LoginLogQueryRequest request);

    Task<OperationResult> ForceLogoutAllAsync(long userId);

    Task<List<UserLoginLogDto>> GetActiveSessionsAsync(long userId);
}
