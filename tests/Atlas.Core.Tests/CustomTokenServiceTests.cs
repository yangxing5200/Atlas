using Xunit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Atlas.Core.Services;
using Atlas.Core.Security;
using Atlas.Infrastructure.Security;
using Atlas.Core.Tests.Mocks;

namespace Atlas.Core.Tests
{
    public class CustomTokenServiceTests
    {
        private readonly CustomTokenService _tokenService;
        private readonly ICryptoService _cryptoService;
        private readonly MockTokenCacheService _tokenCacheService;

        public CustomTokenServiceTests()
        {
            // Setup CryptoService
            var cryptoOptions = Options.Create(new CryptoOptions { Key = "test-secret-key-for-testing-32chars!" });
            _cryptoService = new CryptoService(cryptoOptions);

            // Setup MemoryCache
            var cacheOptions = new MemoryCacheOptions();
            var memoryCache = new MemoryCache(cacheOptions);

            // Setup TokenOptions
            var tokenOptions = Options.Create(new TokenOptions
            {
                SecretKey = "test-token-secret-key-for-testing",
                ExpirationMinutes = 60
            });

            // Setup mock TokenCacheService
            _tokenCacheService = new MockTokenCacheService();

            // Setup logger
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<CustomTokenService>();

            var scopeFactory = new MockServiceScopeFactory(_tokenCacheService);

            _tokenService = new CustomTokenService(
                _cryptoService,
                memoryCache,
                logger,
                tokenOptions,
                scopeFactory);
        }

        [Fact]
        public void GenerateToken_WithTokenInfo_ShouldPreserveTokenVersion()
        {
            // Arrange
            var expectedTokenVersion = 5;
            var user = new TestCurrentUserService
            {
                UserId = 1001,
                TenantId = 1,
                StoreId = 100
            };

            var tokenInfo = TokenInfo.Create(user, 60, expectedTokenVersion);

            // Act
            var token = _tokenService.GenerateToken(tokenInfo);
            var validatedTokenInfo = _tokenService.ValidateToken(token);

            // Assert
            Assert.NotNull(validatedTokenInfo);
            Assert.Equal(expectedTokenVersion, validatedTokenInfo.TokenVersion);
            Assert.Equal(user.UserId, validatedTokenInfo.UserId);
            Assert.Equal(user.TenantId, validatedTokenInfo.TenantId);
            Assert.Equal(user.StoreId, validatedTokenInfo.StoreId);
        }

        [Fact]
        public void GenerateToken_WithICurrentIdentity_ShouldUseDefaultTokenVersion()
        {
            // Arrange
            var user = new TestCurrentUserService
            {
                UserId = 1001,
                TenantId = 1,
                StoreId = 100
            };

            // Act
            var token = _tokenService.GenerateToken(user);
            var validatedTokenInfo = _tokenService.ValidateToken(token);

            // Assert
            Assert.NotNull(validatedTokenInfo);
            Assert.Equal(1, validatedTokenInfo.TokenVersion); // Default value
        }

        [Fact]
        public void GenerateToken_WithTokenInfo_NullInput_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _tokenService.GenerateToken((TokenInfo)null!));
        }

        [Fact]
        public void GenerateToken_WithDifferentTokenVersions_ShouldPreserveEachVersion()
        {
            // Arrange
            var user = new TestCurrentUserService
            {
                UserId = 1001,
                TenantId = 1,
                StoreId = 100
            };

            // Test with version 1
            var tokenInfo1 = TokenInfo.Create(user, 60, 1);
            var token1 = _tokenService.GenerateToken(tokenInfo1);
            var validated1 = _tokenService.ValidateToken(token1);

            // Test with version 10
            var tokenInfo10 = TokenInfo.Create(user, 60, 10);
            var token10 = _tokenService.GenerateToken(tokenInfo10);
            var validated10 = _tokenService.ValidateToken(token10);

            // Test with version 100
            var tokenInfo100 = TokenInfo.Create(user, 60, 100);
            var token100 = _tokenService.GenerateToken(tokenInfo100);
            var validated100 = _tokenService.ValidateToken(token100);

            // Assert
            Assert.NotNull(validated1);
            Assert.Equal(1, validated1.TokenVersion);

            Assert.NotNull(validated10);
            Assert.Equal(10, validated10.TokenVersion);

            Assert.NotNull(validated100);
            Assert.Equal(100, validated100.TokenVersion);
        }

        [Fact]
        public async Task ValidateTokenAsync_WithRevokedSession_ShouldReturnNull()
        {
            var user = new TestCurrentUserService
            {
                UserId = 1001,
                TenantId = 1,
                StoreId = 100
            };
            var tokenInfo = TokenInfo.Create(user, 60, 1);
            var token = _tokenService.GenerateToken(tokenInfo);

            await _tokenCacheService.InvalidateSessionAsync(tokenInfo.SessionId);

            Assert.Null(await _tokenService.ValidateTokenAsync(token));
        }

        [Fact]
        public async Task ValidateTokenAsync_WithTokenVersionMismatch_ShouldReturnNull()
        {
            var user = new TestCurrentUserService
            {
                UserId = 1001,
                TenantId = 1,
                StoreId = 100
            };
            var tokenInfo = TokenInfo.Create(user, 60, 1);
            var token = _tokenService.GenerateToken(tokenInfo);

            await _tokenCacheService.SetUserTokenVersionAsync(user.UserId!.Value, 2);

            Assert.Null(await _tokenService.ValidateTokenAsync(token));
        }
    }

    // Mock implementations for testing
    internal class MockTokenCacheService : ITokenCacheService
    {
        private readonly Dictionary<long, int> _tokenVersions = new();
        private readonly HashSet<string> _invalidSessions = new();

        public Task<int?> GetUserTokenVersionAsync(long userId, CancellationToken ct = default)
        {
            int? result = _tokenVersions.TryGetValue(userId, out var version) ? version : null;
            return Task.FromResult(result);
        }

        public Task SetUserTokenVersionAsync(long userId, int version, CancellationToken ct = default)
        {
            _tokenVersions[userId] = version;
            return Task.CompletedTask;
        }

        public Task InvalidateUserTokensAsync(long userId, CancellationToken ct = default)
        {
            _tokenVersions.Remove(userId);
            return Task.CompletedTask;
        }

        public Task<bool> IsSessionValidAsync(string sessionId, CancellationToken ct = default)
        {
            return Task.FromResult(!_invalidSessions.Contains(sessionId));
        }

        public Task InvalidateSessionAsync(string sessionId, CancellationToken ct = default)
        {
            _invalidSessions.Add(sessionId);
            return Task.CompletedTask;
        }
    }

    internal class MockServiceProvider : IServiceProvider
    {
        private readonly ITokenCacheService _tokenCacheService;

        public MockServiceProvider(ITokenCacheService tokenCacheService)
        {
            _tokenCacheService = tokenCacheService;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ITokenCacheService))
                return _tokenCacheService;
            if (serviceType == typeof(IServiceScopeFactory))
                return new MockServiceScopeFactory(_tokenCacheService);
            return null;
        }
    }

    internal class MockServiceScopeFactory : IServiceScopeFactory
    {
        private readonly ITokenCacheService _tokenCacheService;

        public MockServiceScopeFactory(ITokenCacheService tokenCacheService)
        {
            _tokenCacheService = tokenCacheService;
        }

        public IServiceScope CreateScope()
        {
            return new MockServiceScope(_tokenCacheService);
        }
    }

    internal class MockServiceScope : IServiceScope
    {
        private readonly ITokenCacheService _tokenCacheService;

        public MockServiceScope(ITokenCacheService tokenCacheService)
        {
            _tokenCacheService = tokenCacheService;
        }

        public IServiceProvider ServiceProvider => new MockServiceProvider(_tokenCacheService);

        public void Dispose()
        {
        }
    }
}
