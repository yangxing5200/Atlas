// Keys/CacheKeyGeneratorTests.cs
using System;
using System.Collections.Generic;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Keys.Generators;
using FluentAssertions;
using Xunit;

namespace Atlas.Data.Tests.CacheTest
{
    public class CacheKeyGeneratorTests
    {
        private readonly CacheKeyGenerator _sut;

        public CacheKeyGeneratorTests()
        {
            _sut = new CacheKeyGenerator();
        }

        [Fact]
        public void GenerateGlobalKey_ReturnsCorrectFormat()
        {
            // Arrange
            var baseKey = "my-key";

            // Act
            var result = _sut.GenerateGlobalKey(baseKey);

            // Assert
            result.Should().Be("G:my-key");
        }

        [Fact]
        public void GenerateTenantKey_WithTenantId_ReturnsCorrectFormat()
        {
            // Arrange
            var baseKey = "my-key";
            var tenantId = "tenant123";

            // Act
            var result = _sut.GenerateTenantKey(baseKey, tenantId);

            // Assert
            result.Should().Be("T:tenant123:my-key");
        }

        [Fact]
        public void GenerateStoreKey_WithTenantAndStoreId_ReturnsCorrectFormat()
        {
            // Arrange
            var baseKey = "my-key";
            var tenantId = "tenant123";
            var storeId = "store456";

            // Act
            var result = _sut.GenerateStoreKey(baseKey, tenantId, storeId);

            // Assert
            result.Should().Be("S:tenant123:store456:my-key");
        }

        [Fact]
        public void GenerateUserKey_WithAllIds_ReturnsCorrectFormat()
        {
            // Arrange
            var baseKey = "my-key";
            var tenantId = "tenant123";
            var userId = "user789";

            // Act
            var result = _sut.GenerateUserKey(baseKey, tenantId, userId);

            // Assert
            result.Should().Be("U:tenant123:user789:my-key");
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData(null)]
        public void GenerateGlobalKey_WithInvalidBaseKey_ThrowsException(string invalidKey)
        {
            // Act & Assert
            var act = () => _sut.GenerateGlobalKey(invalidKey);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void GenerateKey_WithGlobalScope_ReturnsGlobalKey()
        {
            // Arrange
            var baseKey = "my-key";
            var scope = CacheScope.Global;

            // Act
            var result = _sut.GenerateKey(baseKey, scope, null);

            // Assert
            result.Should().Be("G:my-key");
        }

        [Fact]
        public void GenerateKey_WithTenantScope_ReturnsTenantKey()
        {
            // Arrange
            var baseKey = "my-key";
            var scope = CacheScope.Tenant;
            var scopeValues = new Dictionary<string, string>
            {
                ["TenantId"] = "tenant123"
            };

            // Act
            var result = _sut.GenerateKey(baseKey, scope, scopeValues);

            // Assert
            result.Should().Be("T:tenant123:my-key");
        }

        [Fact]
        public void GenerateKey_WithStoreScope_ReturnsStoreKey()
        {
            // Arrange
            var baseKey = "my-key";
            var scope = CacheScope.Store;
            var scopeValues = new Dictionary<string, string>
            {
                ["TenantId"] = "tenant123",
                ["StoreId"] = "store456"
            };

            // Act
            var result = _sut.GenerateKey(baseKey, scope, scopeValues);

            // Assert
            result.Should().Be("S:tenant123:store456:my-key");
        }
    }
}