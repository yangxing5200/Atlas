// Tags/TagManagerTests.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Tags;
using FluentAssertions;
using Moq;
using Xunit;

namespace Atlas.Data.Tests.CacheTest
{
    public class TagManagerTests
    {
        private readonly Mock<ITagVersionStore> _versionStoreMock;
        private readonly TagManager _sut;

        public TagManagerTests()
        {
            _versionStoreMock = new Mock<ITagVersionStore>();
            _sut = new TagManager(_versionStoreMock.Object);
        }

        [Fact]
        public async Task GetTagVersionAsync_ReturnsVersionFromStore()
        {
            // Arrange
            var tag = "test-tag";
            var expectedVersion = 42L;

            _versionStoreMock
                .Setup(x => x.GetVersionAsync(tag, default))
                .ReturnsAsync(expectedVersion);

            // Act
            var result = await _sut.GetTagVersionAsync(tag);

            // Assert
            result.Should().Be(expectedVersion);
        }

        [Fact]
        public async Task GetTagVersionsAsync_ReturnsMultipleVersions()
        {
            // Arrange
            var tags = new[] { "tag1", "tag2", "tag3" };
            var expectedVersions = new Dictionary<string, long>
            {
                ["tag1"] = 1,
                ["tag2"] = 2,
                ["tag3"] = 3
            };

            _versionStoreMock
                .Setup(x => x.GetVersionsAsync(tags, default))
                .ReturnsAsync(expectedVersions);

            // Act
            var result = await _sut.GetTagVersionsAsync(tags);

            // Assert
            result.Should().BeEquivalentTo(expectedVersions);
        }

        [Fact]
        public async Task InvalidateTagAsync_IncrementsVersion()
        {
            // Arrange
            var tag = "test-tag";

            // Act
            await _sut.InvalidateTagAsync(tag);

            // Assert
            _versionStoreMock.Verify(
                x => x.IncrementVersionAsync(tag, default),
                Times.Once
            );
        }

        [Fact]
        public async Task InvalidateTagsAsync_IncrementsMultipleVersions()
        {
            // Arrange
            var tags = new[] { "tag1", "tag2" };

            // Act
            await _sut.InvalidateTagsAsync(tags);

            // Assert
            _versionStoreMock.Verify(
                x => x.IncrementVersionsAsync(tags, default),
                Times.Once
            );
        }

        [Fact]
        public async Task GetAllTagsAsync_ReturnsAllTags()
        {
            // Arrange
            var expectedTags = new[] { "tag1", "tag2", "tag3" };

            _versionStoreMock
                .Setup(x => x.GetAllTagsAsync(default))
                .ReturnsAsync(expectedTags);

            // Act
            var result = await _sut.GetAllTagsAsync();

            // Assert
            result.Should().BeEquivalentTo(expectedTags);
        }
    }
}