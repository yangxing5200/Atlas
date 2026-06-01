using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Infrastructure.Security.Permissions;
using Atlas.Infrastructure.Security.DataMasking;
using Atlas.Models.Requests;
using Atlas.Models.Responses;
using Atlas.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Services;

public sealed class UserSensitiveDataRevealService : IUserSensitiveDataRevealService
{
    private static readonly HashSet<string> AllowedUserFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "phone",
        "email"
    };

    private static readonly HashSet<string> AllowedLoginLogFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ipAddress",
        "sessionId"
    };

    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserLoginLog> _loginLogRepository;
    private readonly ISensitiveDataRevealExecutor _revealExecutor;

    public UserSensitiveDataRevealService(
        IRepository<User> userRepository,
        IRepository<UserLoginLog> loginLogRepository,
        ISensitiveDataRevealExecutor revealExecutor)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _loginLogRepository = loginLogRepository ?? throw new ArgumentNullException(nameof(loginLogRepository));
        _revealExecutor = revealExecutor ?? throw new ArgumentNullException(nameof(revealExecutor));
    }

    public Task<RevealSensitiveFieldsResponse> RevealUserFieldsAsync(
        long userId,
        RevealSensitiveFieldsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _revealExecutor.ExecuteAsync(
            new SensitiveDataRevealContext
            {
                Module = "User",
                EntityType = nameof(User),
                EntityId = userId,
                Fields = request.Fields ?? [],
                Reason = request.Reason,
                TicketNo = request.TicketNo,
                RequiredPermission = AtlasPermissionCodes.UsersSensitiveReveal
            },
            async token =>
            {
                ValidateAllowedFields(request.Fields ?? [], AllowedUserFields);
                var queryBuilder = await _userRepository.QueryAsync(token);
                var user = await queryBuilder
                    .Where(x => x.Id == userId && !x.IsDeleted)
                    .FirstOrDefaultAsync(token);

                if (user == null)
                    throw new InvalidOperationException($"User with ID {userId} not found.");

                return new RevealSensitiveFieldsResponse
                {
                    EntityType = nameof(User),
                    EntityId = userId,
                    Fields = BuildUserFields(user, request.Fields ?? []),
                    RevealedAt = DateTime.UtcNow
                };
            },
            ct);
    }

    public Task<RevealSensitiveFieldsResponse> RevealLoginLogFieldsAsync(
        long loginLogId,
        RevealSensitiveFieldsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _revealExecutor.ExecuteAsync(
            new SensitiveDataRevealContext
            {
                Module = "Audit",
                EntityType = nameof(UserLoginLog),
                EntityId = loginLogId,
                Fields = request.Fields ?? [],
                Reason = request.Reason,
                TicketNo = request.TicketNo,
                RequiredPermission = AtlasPermissionCodes.AuditSensitiveReveal
            },
            async token =>
            {
                ValidateAllowedFields(request.Fields ?? [], AllowedLoginLogFields);
                var queryBuilder = await _loginLogRepository.QueryAsync(token);
                var loginLog = await queryBuilder
                    .Where(x => x.Id == loginLogId)
                    .FirstOrDefaultAsync(token);

                if (loginLog == null)
                    throw new InvalidOperationException($"Login log with ID {loginLogId} not found.");

                return new RevealSensitiveFieldsResponse
                {
                    EntityType = nameof(UserLoginLog),
                    EntityId = loginLogId,
                    Fields = BuildLoginLogFields(loginLog, request.Fields ?? []),
                    RevealedAt = DateTime.UtcNow
                };
            },
            ct);
    }

    private static void ValidateAllowedFields(
        IEnumerable<string> requestedFields,
        ISet<string> allowedFields)
    {
        var fields = requestedFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .ToArray();

        if (fields.Length == 0)
            throw new ArgumentException("At least one reveal field is required.", nameof(requestedFields));

        var unsupportedField = fields.FirstOrDefault(field => !allowedFields.Contains(field));
        if (unsupportedField != null)
            throw new ArgumentException($"Unsupported reveal field: {unsupportedField}", nameof(requestedFields));
    }

    private static Dictionary<string, string?> BuildUserFields(User user, IEnumerable<string> fields)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            result[field] = field.ToLowerInvariant() switch
            {
                "phone" => user.Phone,
                "email" => user.Email,
                _ => throw new ArgumentException($"Unsupported reveal field: {field}")
            };
        }

        return result;
    }

    private static Dictionary<string, string?> BuildLoginLogFields(UserLoginLog loginLog, IEnumerable<string> fields)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            result[field] = field.ToLowerInvariant() switch
            {
                "ipaddress" => loginLog.IpAddress,
                "sessionid" => loginLog.SessionId,
                _ => throw new ArgumentException($"Unsupported reveal field: {field}")
            };
        }

        return result;
    }
}
