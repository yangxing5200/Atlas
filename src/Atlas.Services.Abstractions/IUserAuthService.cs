using Atlas.Models.DTOs;
using Atlas.Models.Requests;
using Atlas.Models.Responses;

namespace Atlas.Services.Abstractions;

public interface IUserAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string? userAgent);

    Task<LoginResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress, string? userAgent);

    Task<SwitchStoreResponse> SwitchStoreAsync(long userId, SwitchStoreRequest request);

    Task<List<StoreInfoDto>> GetAccessibleStoresAsync(long userId);

    Task<OperationResult> LogoutAsync(string sessionId);
}
