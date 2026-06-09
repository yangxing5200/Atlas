using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;

namespace Atlas.Services;

public interface IUserLoginAuditWriter
{
    Task LogSuccessAsync(User user, long tenantId, long storeId, string sessionId, string ipAddress, string? userAgent, long expiresAt);

    Task LogFailureAsync(long userId, long tenantId, long? storeId, string ipAddress, string? userAgent, string reason);
}

public sealed class UserLoginAuditWriter : IUserLoginAuditWriter
{
    private readonly IRepository<UserLoginLog> _userLoginLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserLoginAuditWriter(
        IRepository<UserLoginLog> userLoginLogRepository,
        IUnitOfWork unitOfWork)
    {
        _userLoginLogRepository = userLoginLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task LogSuccessAsync(User user, long tenantId, long storeId, string sessionId, string ipAddress, string? userAgent, long expiresAt)
    {
        var loginLog = new UserLoginLog
        {
            TenantId = tenantId,
            UserId = user.Id,
            SessionId = sessionId,
            TokenVersion = user.TokenVersion,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceType = ParseDeviceType(userAgent),
            Browser = ParseBrowser(userAgent),
            StoreId = storeId,
            LoginMethod = "Password",
            IsSuccess = true,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresAt).UtcDateTime
        };

        await _userLoginLogRepository.AddAsync(loginLog, tenantId);
        await _unitOfWork.SaveChangesAsync(tenantId);
    }

    public async Task LogFailureAsync(long userId, long tenantId, long? storeId, string ipAddress, string? userAgent, string reason)
    {
        var loginLog = new UserLoginLog
        {
            TenantId = tenantId,
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceType = ParseDeviceType(userAgent),
            Browser = ParseBrowser(userAgent),
            StoreId = storeId,
            LoginMethod = "Password",
            IsSuccess = false,
            FailureReason = reason
        };

        await _userLoginLogRepository.AddAsync(loginLog, tenantId);
        await _unitOfWork.SaveChangesAsync(tenantId);
    }

    private static string? ParseDeviceType(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return null;
        }

        if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
        {
            return "Mobile";
        }

        if (userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase))
        {
            return "Tablet";
        }

        return "Desktop";
    }

    private static string? ParseBrowser(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return null;
        }

        if (userAgent.Contains("Chrome"))
        {
            return "Chrome";
        }

        if (userAgent.Contains("Firefox"))
        {
            return "Firefox";
        }

        if (userAgent.Contains("Safari"))
        {
            return "Safari";
        }

        if (userAgent.Contains("Edge"))
        {
            return "Edge";
        }

        return "Unknown";
    }
}
