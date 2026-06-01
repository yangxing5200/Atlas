using System.Security.Cryptography;
using System.Text;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Security;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private const string TokenPrefix = "rt";
    private readonly IRepository<RefreshToken> _refreshTokens;
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly ITokenCacheService _tokenCacheService;
    private readonly IIdGenerator _idGenerator;
    private readonly TokenOptions _options;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        IRepository<RefreshToken> refreshTokens,
        IRepository<User> users,
        IUnitOfWork unitOfWork,
        ITokenService tokenService,
        ITokenCacheService tokenCacheService,
        IIdGenerator idGenerator,
        IOptions<TokenOptions> options,
        ILogger<RefreshTokenService> logger)
    {
        _refreshTokens = refreshTokens ?? throw new ArgumentNullException(nameof(refreshTokens));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _tokenCacheService = tokenCacheService ?? throw new ArgumentNullException(nameof(tokenCacheService));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IssuedRefreshToken> IssueAsync(
        TokenInfo accessTokenInfo,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(accessTokenInfo);
        var tenantId = accessTokenInfo.TenantId.GetValueOrDefault();
        var userId = accessTokenInfo.UserId.GetValueOrDefault();
        if (tenantId <= 0 || userId <= 0 || string.IsNullOrWhiteSpace(accessTokenInfo.SessionId))
            throw new InvalidOperationException("A refresh token requires tenant, user, and session context.");

        var issued = CreateRefreshToken(accessTokenInfo, ipAddress, userAgent);
        await _refreshTokens.AddAsync(issued.Entity, tenantId, ct);
        await _unitOfWork.SaveChangesAsync(tenantId, ct);

        return new IssuedRefreshToken(issued.Entity.Id, issued.Token, issued.Entity.ExpiresAtUtc);
    }

    public async Task<RefreshTokenExchangeResult> ExchangeAsync(
        string refreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        if (!TryParse(refreshToken, out var parsed))
            return RefreshTokenExchangeResult.Failed("Invalid refresh token.");

        var now = DateTime.UtcNow;
        var query = await _refreshTokens.QueryTrackingAsync(parsed.TenantId, ct);
        var storedToken = await query
            .Where(x => x.Id == parsed.TokenId &&
                        x.TenantId == parsed.TenantId &&
                        x.UserId == parsed.UserId &&
                        x.TokenHash == HashToken(refreshToken))
            .FirstOrDefaultAsync(ct);

        if (storedToken == null || !storedToken.IsActive(now))
            return RefreshTokenExchangeResult.Failed("Refresh token has expired or been revoked.");

        if (!_tokenCacheService.IsSessionValid(storedToken.SessionId))
            return RefreshTokenExchangeResult.Failed("Session has been revoked.");

        var userQuery = await _users.QueryAsync(parsed.TenantId, ct);
        var user = await userQuery
            .Where(x => x.Id == parsed.UserId &&
                        x.TenantId == parsed.TenantId &&
                        !x.IsDeleted &&
                        x.Status == UserStatus.Active)
            .FirstOrDefaultAsync(ct);

        if (user == null)
            return RefreshTokenExchangeResult.Failed("User is not active.");

        _tokenCacheService.SetUserTokenVersion(user.Id, user.TokenVersion);

        var accessTokenInfo = TokenInfo.Create(new RefreshTokenIdentity
        {
            UserId = user.Id,
            UserName = user.UserName,
            TenantId = parsed.TenantId,
            StoreId = storedToken.StoreId ?? user.DefaultStoreId ?? 0
        }, _options.ExpirationMinutes, user.TokenVersion);
        var accessToken = _tokenService.GenerateToken(accessTokenInfo);

        var replacement = CreateRefreshToken(accessTokenInfo, ipAddress, userAgent);
        await _refreshTokens.AddAsync(replacement.Entity, parsed.TenantId, ct);
        storedToken.RevokedAtUtc = now;
        storedToken.RevokedReason = "Rotated";
        storedToken.ReplacedByTokenId = replacement.Entity.Id;
        await _unitOfWork.SaveChangesAsync(parsed.TenantId, ct);

        return new RefreshTokenExchangeResult
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = replacement.Token,
            AccessTokenExpiresAtUtc = accessTokenInfo.GetExpiryDateTime(),
            RefreshTokenExpiresAtUtc = replacement.Entity.ExpiresAtUtc,
            ExpiresIn = _options.ExpirationMinutes * 60,
            TenantId = parsed.TenantId,
            UserId = user.Id,
            StoreId = accessTokenInfo.StoreId
        };
    }

    public async Task RevokeSessionAsync(
        long tenantId,
        string sessionId,
        string reason,
        CancellationToken ct = default)
    {
        if (tenantId <= 0 || string.IsNullOrWhiteSpace(sessionId))
            return;

        var query = await _refreshTokens.QueryTrackingAsync(tenantId, ct);
        var tokens = await query
            .Where(x => x.TenantId == tenantId &&
                        x.SessionId == sessionId &&
                        x.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            token.RevokedReason = reason;
        }

        if (tokens.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(tenantId, ct);
            _logger.LogInformation("Revoked {Count} refresh tokens for session {SessionId}.", tokens.Count, sessionId);
        }
    }

    public async Task RevokeUserAsync(
        long tenantId,
        long userId,
        string reason,
        CancellationToken ct = default)
    {
        if (tenantId <= 0 || userId <= 0)
            return;

        var query = await _refreshTokens.QueryTrackingAsync(tenantId, ct);
        var tokens = await query
            .Where(x => x.TenantId == tenantId &&
                        x.UserId == userId &&
                        x.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            token.RevokedReason = reason;
        }

        if (tokens.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(tenantId, ct);
            _logger.LogInformation("Revoked {Count} refresh tokens for user {UserId}.", tokens.Count, userId);
        }
    }

    private static string CreateTokenString(long tenantId, long userId, long tokenId)
    {
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = ToBase64Url(secretBytes);
        return $"{TokenPrefix}.{tenantId}.{userId}.{tokenId}.{secret}";
    }

    private CreatedRefreshToken CreateRefreshToken(TokenInfo accessTokenInfo, string? ipAddress, string? userAgent)
    {
        var tenantId = accessTokenInfo.TenantId.GetValueOrDefault();
        var userId = accessTokenInfo.UserId.GetValueOrDefault();
        var tokenId = _idGenerator.NextId();
        var token = CreateTokenString(tenantId, userId, tokenId);
        var entity = new RefreshToken
        {
            Id = tokenId,
            TenantId = tenantId,
            UserId = userId,
            StoreId = accessTokenInfo.StoreId,
            SessionId = accessTokenInfo.SessionId,
            TokenHash = HashToken(token),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays),
            CreatedByIp = ipAddress,
            UserAgent = userAgent
        };

        return new CreatedRefreshToken(entity, token);
    }

    public static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    public static bool TryParse(string token, out RefreshTokenDescriptor descriptor)
    {
        descriptor = default;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var parts = token.Split('.');
        if (parts.Length != 5 ||
            !string.Equals(parts[0], TokenPrefix, StringComparison.Ordinal))
            return false;

        if (!long.TryParse(parts[1], out var tenantId) ||
            !long.TryParse(parts[2], out var userId) ||
            !long.TryParse(parts[3], out var tokenId) ||
            tenantId <= 0 ||
            userId <= 0 ||
            tokenId <= 0 ||
            string.IsNullOrWhiteSpace(parts[4]))
        {
            return false;
        }

        descriptor = new RefreshTokenDescriptor(tenantId, userId, tokenId);
        return true;
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class RefreshTokenIdentity : Atlas.Core.Services.ICurrentIdentity
    {
        public long? UserId { get; init; }
        public string UserName { get; init; } = string.Empty;
        public long? TenantId { get; init; }
        public long? StoreId { get; init; }
        public bool IsAuthenticated => UserId.HasValue;
        public string? SessionId { get; init; }
    }

    private sealed record CreatedRefreshToken(RefreshToken Entity, string Token);
}

public readonly record struct RefreshTokenDescriptor(long TenantId, long UserId, long TokenId);
