using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Invalidation;
using Atlas.Infrastructure.Caching.Tests.Helpers;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Invalidation
{
    public class CacheInvalidatorTests
    {
        private readonly Mock<ICacheProvider> _mockProvider;
        private readonly Mock<ITagManager> _mockTagManager;
        private readonly CacheInvalidator _invalidator;

        public CacheInvalidatorTests()
        {
            _mockProvider = TestHelpers.CreateMockProvider();
            _mockTagManager = new Mock<ITagManager>();
            _invalidator = new CacheInvalidator(_mockProvider.Object, _mockTagManager.Object);
        }

        #region InvalidateByKeyAsync Tests

        [Fact]
        public async Task InvalidateByKeyAsync_CallsProviderRemove()
        {
            // Arrange
            var key = "product:123";

            _mockProvider.Setup(x => x.RemoveAsync(key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _invalidator.InvalidateByKeyAsync(key);

            // Assert
            _mockProvider.Verify(x => x.RemoveAsync(key, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region InvalidateByKeysAsync Tests

        [Fact]
        public async Task InvalidateByKeysAsync_CallsProviderRemoveMany()
        {
            // Arrange
            var keys = new[] { "product:1", "product:2", "product:3" };

            _mockProvider.Setup(x => x.RemoveManyAsync(keys, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Act
            await _invalidator.InvalidateByKeysAsync(keys);

            // Assert
            _mockProvider.Verify(x => x.RemoveManyAsync(keys, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region InvalidateByTagAsync Tests

        [Fact]
        public async Task InvalidateByTagAsync_CallsTagManagerInvalidate()
        {
            // Arrange
            var tag = "product";

            _mockTagManager.Setup(x => x.InvalidateTagAsync(tag, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _invalidator.InvalidateByTagAsync(tag);

            // Assert
            _mockTagManager.Verify(x => x.InvalidateTagAsync(tag, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region InvalidateByTagsAsync Tests

        [Fact]
        public async Task InvalidateByTagsAsync_CallsTagManagerInvalidateMany()
        {
            // Arrange
            var tags = new[] { "product", "category", "brand" };

            _mockTagManager.Setup(x => x.InvalidateTagsAsync(tags, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _invalidator.InvalidateByTagsAsync(tags);

            // Assert
            _mockTagManager.Verify(x => x.InvalidateTagsAsync(tags, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region InvalidateByScopeAsync Tests

        [Theory]
        [InlineData(CacheScope.Global, null, "G:*")]
        [InlineData(CacheScope.Tenant, "tenant-001", "T:tenant-001:*")]
        [InlineData(CacheScope.Store, "tenant-001:store-001", "S:tenant-001:store-001:*")]
        [InlineData(CacheScope.User, "tenant-001:user-001", "U:tenant-001:user-001:*")]
        public async Task InvalidateByScopeAsync_CallsProviderWithCorrectPattern(
            CacheScope scope,
            string? scopeId,
            string expectedPattern)
        {
            // Arrange
            _mockProvider.Setup(x => x.GetKeysByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            // Act
            await _invalidator.InvalidateByScopeAsync(scope, scopeId);

            // Assert
            _mockProvider.Verify(x => x.GetKeysByPatternAsync(
                expectedPattern,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(CacheScope.Tenant, null)]
        [InlineData(CacheScope.Tenant, "tenant-001:store-001")]
        [InlineData(CacheScope.Store, "store-001")]
        [InlineData(CacheScope.User, "user-001")]
        public async Task InvalidateByScopeAsync_WithInvalidScopeId_ThrowsArgumentException(
            CacheScope scope,
            string? scopeId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _invalidator.InvalidateByScopeAsync(scope, scopeId));
        }

        [Fact]
        public async Task InvalidateByScopeAsync_WhenKeysFound_RemovesThem()
        {
            // Arrange
            var scope = CacheScope.Tenant;
            var scopeId = "tenant-001";
            var matchingKeys = new[] { "T:tenant-001:product:1", "T:tenant-001:product:2" };

            _mockProvider.Setup(x => x.GetKeysByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(matchingKeys);

            _mockProvider.Setup(x => x.RemoveManyAsync(matchingKeys, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            // Act
            await _invalidator.InvalidateByScopeAsync(scope, scopeId);

            // Assert
            _mockProvider.Verify(x => x.RemoveManyAsync(
                It.Is<IEnumerable<string>>(keys => keys.SequenceEqual(matchingKeys)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateByScopeAsync_WhenNoKeysFound_DoesNotCallRemove()
        {
            // Arrange
            var scope = CacheScope.Tenant;
            var scopeId = "tenant-001";

            _mockProvider.Setup(x => x.GetKeysByPatternAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<string>());

            // Act
            await _invalidator.InvalidateByScopeAsync(scope, scopeId);

            // Assert
            _mockProvider.Verify(x => x.RemoveManyAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        #endregion

        #region InvalidateByPatternAsync Tests

        [Fact]
        public async Task InvalidateByPatternAsync_WithMatchingKeys_RemovesThem()
        {
            // Arrange
            var pattern = "product:*";
            var matchingKeys = new[] { "product:1", "product:2", "product:3" };

            _mockProvider.Setup(x => x.GetKeysByPatternAsync(pattern, It.IsAny<CancellationToken>()))
                .ReturnsAsync(matchingKeys);

            _mockProvider.Setup(x => x.RemoveManyAsync(matchingKeys, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Act
            await _invalidator.InvalidateByPatternAsync(pattern);

            // Assert
            _mockProvider.Verify(x => x.GetKeysByPatternAsync(pattern, It.IsAny<CancellationToken>()), Times.Once);
            _mockProvider.Verify(x => x.RemoveManyAsync(
                It.Is<IEnumerable<string>>(keys => keys.SequenceEqual(matchingKeys)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateByPatternAsync_WithNoMatches_DoesNotCallRemove()
        {
            // Arrange
            var pattern = "product:*";

            _mockProvider.Setup(x => x.GetKeysByPatternAsync(pattern, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<string>());

            // Act
            await _invalidator.InvalidateByPatternAsync(pattern);

            // Assert
            _mockProvider.Verify(x => x.GetKeysByPatternAsync(pattern, It.IsAny<CancellationToken>()), Times.Once);
            _mockProvider.Verify(x => x.RemoveManyAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task InvalidateByPatternAsync_WithComplexPattern_HandlesCorrectly()
        {
            // Arrange
            var pattern = "T:tenant-001:product:category:*";
            var matchingKeys = new[] 
            { 
                "T:tenant-001:product:category:electronics",
                "T:tenant-001:product:category:books"
            };

            _mockProvider.Setup(x => x.GetKeysByPatternAsync(pattern, It.IsAny<CancellationToken>()))
                .ReturnsAsync(matchingKeys);

            _mockProvider.Setup(x => x.RemoveManyAsync(matchingKeys, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            // Act
            await _invalidator.InvalidateByPatternAsync(pattern);

            // Assert
            _mockProvider.Verify(x => x.RemoveManyAsync(
                It.Is<IEnumerable<string>>(keys => keys.Count() == 2),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullProvider_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CacheInvalidator(null!, _mockTagManager.Object));
        }

        [Fact]
        public void Constructor_WithNullTagManager_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CacheInvalidator(_mockProvider.Object, null!));
        }

        #endregion
    }
}
