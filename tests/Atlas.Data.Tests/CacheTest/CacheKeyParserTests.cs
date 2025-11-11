// Keys/CacheKeyParserTests.cs
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Keys.Parsers;
using FluentAssertions;
using Xunit;

namespace Atlas.Data.Tests.CacheTest
{
    public class CacheKeyParserTests
    {
        private readonly CacheKeyParser _sut;

        public CacheKeyParserTests()
        {
            _sut = new CacheKeyParser();
        }

        [Fact]
        public void Parse_GlobalKey_ReturnsCorrectMetadata()
        {
            // Arrange
            var key = "G:my-key";

            // Act
            var result = _sut.Parse(key);

            // Assert
            result.Scope.Should().Be(CacheScope.Global);
            result.BaseKey.Should().Be("my-key");
            result.TenantId.Should().BeNull();
            result.StoreId.Should().BeNull();
            result.UserId.Should().BeNull();
        }

        [Fact]
        public void Parse_TenantKey_ReturnsCorrectMetadata()
        {
            // Arrange
            var key = "T:tenant123:my-key";

            // Act
            var result = _sut.Parse(key);

            // Assert
            result.Scope.Should().Be(CacheScope.Tenant);
            result.TenantId.Should().Be("tenant123");
            result.BaseKey.Should().Be("my-key");
            result.StoreId.Should().BeNull();
            result.UserId.Should().BeNull();
        }

        [Fact]
        public void Parse_StoreKey_ReturnsCorrectMetadata()
        {
            // Arrange
            var key = "S:tenant123:store456:my-key";

            // Act
            var result = _sut.Parse(key);

            // Assert
            result.Scope.Should().Be(CacheScope.Store);
            result.TenantId.Should().Be("tenant123");
            result.StoreId.Should().Be("store456");
            result.BaseKey.Should().Be("my-key");
            result.UserId.Should().BeNull();
        }

        [Fact]
        public void Parse_UserKey_ReturnsCorrectMetadata()
        {
            // Arrange
            var key = "U:tenant123:user789:my-key";

            // Act
            var result = _sut.Parse(key);

            // Assert
            result.Scope.Should().Be(CacheScope.User);
            result.TenantId.Should().Be("tenant123");
            result.UserId.Should().Be("user789");
            result.BaseKey.Should().Be("my-key");
        }

        [Theory]
        [InlineData("invalid-key")]
        [InlineData("X:tenant:key")]
        [InlineData("")]
        public void Parse_InvalidKey_ThrowsException(string invalidKey)
        {
            // Act & Assert
            var act = () => _sut.Parse(invalidKey);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void TryParse_ValidKey_ReturnsTrue()
        {
            // Arrange
            var key = "G:my-key";

            // Act
            var result = _sut.TryParse(key, out var metadata);

            // Assert
            result.Should().BeTrue();
            metadata.Should().NotBeNull();
        }

        [Fact]
        public void TryParse_InvalidKey_ReturnsFalse()
        {
            // Arrange
            var key = "invalid-key";

            // Act
            var result = _sut.TryParse(key, out var metadata);

            // Assert
            result.Should().BeFalse();
            metadata.Should().BeNull();
        }

        [Fact]
        public void ExtractTenantId_FromTenantKey_ReturnsId()
        {
            // Arrange
            var key = "T:tenant123:my-key";

            // Act
            var result = _sut.ExtractTenantId(key);

            // Assert
            result.Should().Be("tenant123");
        }

        [Fact]
        public void ExtractStoreId_FromStoreKey_ReturnsId()
        {
            // Arrange
            var key = "S:tenant123:store456:my-key";

            // Act
            var result = _sut.ExtractStoreId(key);

            // Assert
            result.Should().Be("store456");
        }
    }
}