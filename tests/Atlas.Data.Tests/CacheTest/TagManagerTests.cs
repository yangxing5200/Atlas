using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Tags;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Tags
{
    public class TagManagerTests
    {
        private readonly Mock<ITagVersionStore> _mockStore;
        private readonly TagManager _tagManager;

        public TagManagerTests()
        {
            _mockStore = new Mock<ITagVersionStore>();
            _tagManager = new TagManager(_mockStore.Object);
        }

        #region GetTagVersionAsync Tests

        [Fact]
        public async Task GetTagVersionAsync_WithValidTag_ReturnsVersion()
        {
            // Arrange
            var tag = "product";
            var expectedVersion = 5L;

            _mockStore.Setup(x => x.GetVersionAsync(tag, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedVersion);

            // Act
            var version = await _tagManager.GetTagVersionAsync(tag);

            // Assert
            version.Should().Be(expectedVersion);
            _mockStore.Verify(x => x.GetVersionAsync(tag, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetTagVersionAsync_WithInvalidTag_ThrowsArgumentException(string? tag)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _tagManager.GetTagVersionAsync(tag!));
        }

        #endregion

        #region GetTagVersionsAsync Tests

        [Fact]
        public async Task GetTagVersionsAsync_WithMultipleTags_ReturnsAllVersions()
        {
            // Arrange
            var tags = new[] { "product", "category", "brand" };
            var expectedVersions = new Dictionary<string, long>
            {
                { "product", 1L },
                { "category", 2L },
                { "brand", 3L }
            };

            _mockStore.Setup(x => x.GetVersionsAsync(tags, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedVersions);

            // Act
            var versions = await _tagManager.GetTagVersionsAsync(tags);

            // Assert
            versions.Should().NotBeNull();
            versions.Should().HaveCount(3);
            versions["product"].Should().Be(1L);
            versions["category"].Should().Be(2L);
            versions["brand"].Should().Be(3L);
        }

        [Fact]
        public async Task GetTagVersionsAsync_WithEmptyList_ReturnsEmptyDictionary()
        {
            // Arrange
            var tags = Enumerable.Empty<string>();
            _mockStore.Setup(x => x.GetVersionsAsync(tags, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, long>());

            // Act
            var versions = await _tagManager.GetTagVersionsAsync(tags);

            // Assert
            versions.Should().NotBeNull();
            versions.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTagVersionsAsync_WithInvalidTag_ThrowsArgumentException()
        {
            // Arrange
            var tags = new[] { "product", "", "category" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _tagManager.GetTagVersionsAsync(tags));
        }

        #endregion

        #region InvalidateTagAsync Tests

        [Fact]
        public async Task InvalidateTagAsync_WithValidTag_IncrementsVersion()
        {
            // Arrange
            var tag = "product";
            _mockStore.Setup(x => x.IncrementVersionAsync(tag, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2L);

            // Act
            await _tagManager.InvalidateTagAsync(tag);

            // Assert
            _mockStore.Verify(x => x.IncrementVersionAsync(tag, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task InvalidateTagAsync_WithInvalidTag_ThrowsArgumentException(string? tag)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _tagManager.InvalidateTagAsync(tag!));
        }

        #endregion

        #region InvalidateTagsAsync Tests

        [Fact]
        public async Task InvalidateTagsAsync_WithMultipleTags_IncrementsAllVersions()
        {
            // Arrange
            var tags = new[] { "product", "category", "brand" };

            _mockStore.Setup(x => x.IncrementVersionsAsync(tags, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _tagManager.InvalidateTagsAsync(tags);

            // Assert
            _mockStore.Verify(x => x.IncrementVersionsAsync(tags, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateTagsAsync_WithInvalidTag_ThrowsArgumentException()
        {
            // Arrange
            var tags = new[] { "product", null, "category" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _tagManager.InvalidateTagsAsync(tags!));
        }

        #endregion

        #region GetAllTagsAsync Tests

        [Fact]
        public async Task GetAllTagsAsync_ReturnsAllTags()
        {
            // Arrange
            var expectedTags = new[] { "product", "category", "brand" };
            _mockStore.Setup(x => x.GetAllTagsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedTags);

            // Act
            var tags = await _tagManager.GetAllTagsAsync();

            // Assert
            tags.Should().NotBeNull();
            tags.Should().BeEquivalentTo(expectedTags);
        }

        [Fact]
        public async Task GetAllTagsAsync_WithNoTags_ReturnsEmptyList()
        {
            // Arrange
            _mockStore.Setup(x => x.GetAllTagsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<string>());

            // Act
            var tags = await _tagManager.GetAllTagsAsync();

            // Assert
            tags.Should().NotBeNull();
            tags.Should().BeEmpty();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullStore_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TagManager(null!));
        }

        #endregion
    }
}
