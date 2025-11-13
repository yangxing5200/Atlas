using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Keys.Generators;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Keys
{
    public class CacheKeyGeneratorTests
    {
        private readonly CacheKeyGenerator _generator;

        public CacheKeyGeneratorTests()
        {
            _generator = new CacheKeyGenerator();
        }

        #region GenerateGlobalKey Tests

        [Fact]
        public void GenerateGlobalKey_WithValidBaseKey_ReturnsCorrectFormat()
        {
            // Arrange
            var baseKey = "product:123";

            // Act
            var result = _generator.GenerateGlobalKey(baseKey);

            // Assert
            result.Should().Be("G:product:123");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GenerateGlobalKey_WithInvalidBaseKey_ThrowsArgumentException(string? baseKey)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _generator.GenerateGlobalKey(baseKey!));
        }

        #endregion

        #region GenerateTenantKey Tests

        [Fact]
        public void GenerateTenantKey_WithValidParameters_ReturnsCorrectFormat()
        {
            // Arrange
            var baseKey = "product:123";
            var tenantId = "tenant-001";

            // Act
            var result = _generator.GenerateTenantKey(baseKey, tenantId);

            // Assert
            result.Should().Be("T:tenant-001:product:123");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GenerateTenantKey_WithInvalidBaseKey_ThrowsArgumentException(string? baseKey)
        {
            // Arrange
            var tenantId = "tenant-001";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _generator.GenerateTenantKey(baseKey!, tenantId));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GenerateTenantKey_WithInvalidTenantId_ThrowsArgumentException(string? tenantId)
        {
            // Arrange
            var baseKey = "product:123";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _generator.GenerateTenantKey(baseKey, tenantId!));
        }

        [Fact]
        public void GenerateTenantKey_WithTenantIdContainingSeparator_ThrowsArgumentException()
        {
            // Arrange
            var baseKey = "product:123";
            var tenantId = "tenant:001";  // Contains colon separator

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _generator.GenerateTenantKey(baseKey, tenantId));
        }

        #endregion

        #region GenerateStoreKey Tests

        [Fact]
        public void GenerateStoreKey_WithValidParameters_ReturnsCorrectFormat()
        {
            // Arrange
            var baseKey = "product:123";
            var tenantId = "tenant-001";
            var storeId = "store-001";

            // Act
            var result = _generator.GenerateStoreKey(baseKey, tenantId, storeId);

            // Assert
            result.Should().Be("S:tenant-001:store-001:product:123");
        }

        [Theory]
        [InlineData(null, "store-001")]
        [InlineData("", "store-001")]
        [InlineData("   ", "store-001")]
        [InlineData("tenant-001", null)]
        [InlineData("tenant-001", "")]
        [InlineData("tenant-001", "   ")]
        public void GenerateStoreKey_WithInvalidParameters_ThrowsArgumentException(
            string? tenantId,
            string? storeId)
        {
            // Arrange
            var baseKey = "product:123";

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _generator.GenerateStoreKey(baseKey, tenantId!, storeId!));
        }

        #endregion

        #region GenerateUserKey Tests

        [Fact]
        public void GenerateUserKey_WithValidParameters_ReturnsCorrectFormat()
        {
            // Arrange
            var baseKey = "settings:theme";
            var tenantId = "tenant-001";
            var userId = "user-123";

            // Act
            var result = _generator.GenerateUserKey(baseKey, tenantId, userId);

            // Assert
            result.Should().Be("U:tenant-001:user-123:settings:theme");
        }

        [Theory]
        [InlineData(null, "user-123")]
        [InlineData("", "user-123")]
        [InlineData("   ", "user-123")]
        [InlineData("tenant-001", null)]
        [InlineData("tenant-001", "")]
        [InlineData("tenant-001", "   ")]
        public void GenerateUserKey_WithInvalidParameters_ThrowsArgumentException(
            string? tenantId,
            string? userId)
        {
            // Arrange
            var baseKey = "settings:theme";

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _generator.GenerateUserKey(baseKey, tenantId!, userId!));
        }

        #endregion

        #region GenerateKey Tests

        [Fact]
        public void GenerateKey_WithGlobalScope_ReturnsGlobalKey()
        {
            // Arrange
            var baseKey = "product:123";
            var scope = CacheScope.Global;

            // Act
            var result = _generator.GenerateKey(baseKey, scope);

            // Assert
            result.Should().Be("G:product:123");
        }

        [Fact]
        public void GenerateKey_WithTenantScope_ReturnsTenantKey()
        {
            // Arrange
            var baseKey = "product:123";
            var scope = CacheScope.Tenant;
            var scopeValues = new Dictionary<string, string>
            {
                { "TenantId", "tenant-001" }
            };

            // Act
            var result = _generator.GenerateKey(baseKey, scope, scopeValues);

            // Assert
            result.Should().Be("T:tenant-001:product:123");
        }

        [Fact]
        public void GenerateKey_WithStoreScope_ReturnsStoreKey()
        {
            // Arrange
            var baseKey = "product:123";
            var scope = CacheScope.Store;
            var scopeValues = new Dictionary<string, string>
            {
                { "TenantId", "tenant-001" },
                { "StoreId", "store-001" }
            };

            // Act
            var result = _generator.GenerateKey(baseKey, scope, scopeValues);

            // Assert
            result.Should().Be("S:tenant-001:store-001:product:123");
        }

        [Fact]
        public void GenerateKey_WithUserScope_ReturnsUserKey()
        {
            // Arrange
            var baseKey = "settings:theme";
            var scope = CacheScope.User;
            var scopeValues = new Dictionary<string, string>
            {
                { "TenantId", "tenant-001" },
                { "UserId", "user-123" }
            };

            // Act
            var result = _generator.GenerateKey(baseKey, scope, scopeValues);

            // Assert
            result.Should().Be("U:tenant-001:user-123:settings:theme");
        }

        [Fact]
        public void GenerateKey_WithMissingScopeValues_ThrowsArgumentException()
        {
            // Arrange
            var baseKey = "product:123";
            var scope = CacheScope.Tenant;

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _generator.GenerateKey(baseKey, scope, null));
        }

        [Fact]
        public void GenerateKey_WithBaseKeyContainingColons_AllowsIt()
        {
            // Arrange
            var baseKey = "product:category:subcategory:123";
            var scope = CacheScope.Global;

            // Act
            var result = _generator.GenerateKey(baseKey, scope);

            // Assert
            result.Should().Be("G:product:category:subcategory:123");
        }

        #endregion
    }
}
