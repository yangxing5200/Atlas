using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Tests.Helpers;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Models
{
    public class CacheKeyDefinitionTests
    {
        #region Builder Tests

        [Fact]
        public void Create_WithName_ReturnsBuilder()
        {
            // Act
            var builder = CacheKeyDefinition.Create("product:{id}");

            // Assert
            builder.Should().NotBeNull();
        }

        [Fact]
        public void Build_WithMinimalConfig_CreateDefinition()
        {
            // Act
            var definition = CacheKeyDefinition.Create("product:{id}")
                .Build();

            // Assert
            definition.Should().NotBeNull();
            definition.Name.Should().Be("product:{id}");
            definition.Scope.Should().Be(CacheScope.Tenant); // Default scope
            definition.DefaultExpiration.Should().Be(TimeSpan.FromHours(1)); // Default expiration
        }

        [Fact]
        public void Build_WithAllOptions_CreatesDefinition()
        {
            // Arrange
            var expiration = TimeSpan.FromMinutes(30);
            var description = "Product cache definition";

            // Act
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("id")
                .WithExpiration(expiration)
                .EnableL1Cache(true)
                .WithMaxRandomOffset(60)
                .WithDescription(description)
                .AllowNull(true)
                .Build();

            // Assert
            definition.Should().NotBeNull();
            definition.Name.Should().Be("product:{id}");
            definition.Scope.Should().Be(CacheScope.Global);
            definition.InstanceKeyName.Should().Be("id");
            definition.DefaultExpiration.Should().Be(expiration);
            definition.EnableL1Cache.Should().BeTrue();
            definition.MaxRandomOffsetSeconds.Should().Be(60);
            definition.Description.Should().Be(description);
            definition.AllowNull.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_WithInvalidName_ThrowsArgumentException(string? name)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                CacheKeyDefinition.Create(name!).Build());
        }

        #endregion

        #region BuildKey Tests

        [Fact]
        public void BuildKey_WithoutInstanceValue_ReturnsTemplateName()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithInstanceKey("id")
                .Build();

            // Act
            var key = definition.BuildKey();

            // Assert
            key.Should().Be("product:{id}");
        }

        [Fact]
        public void BuildKey_WithInstanceValue_ReplacesPlaceholder()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithInstanceKey("id")
                .Build();

            // Act
            var key = definition.BuildKey(123);

            // Assert
            key.Should().Be("product:123");
        }

        [Fact]
        public void BuildKey_WithStringInstanceValue_ReplacesPlaceholder()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("user:{userId}:settings")
                .WithInstanceKey("userId")
                .Build();

            // Act
            var key = definition.BuildKey("john-doe");

            // Assert
            key.Should().Be("user:john-doe:settings");
        }

        [Fact]
        public void BuildKey_WithNoPlaceholder_ReturnsOriginalName()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("global-config")
                .Build();

            // Act
            var key = definition.BuildKey();

            // Assert
            key.Should().Be("global-config");
        }

        [Fact]
        public void BuildKey_WithMultipleSegments_HandlesCorrectly()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("tenant:products:list:{categoryId}")
                .WithInstanceKey("categoryId")
                .Build();

            // Act
            var key = definition.BuildKey(456);

            // Assert
            key.Should().Be("tenant:products:list:456");
        }

        #endregion

        #region CreateOptions Tests

        [Fact]
        public void CreateOptions_WithDefaultSettings_ReturnsOptions()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();
            var context = TestHelpers.CreateScopeContext();

            // Act
            var options = definition.CreateOptions(context);

            // Assert
            options.Should().NotBeNull();
            options.AbsoluteExpiration.Should().NotBeNull();
            options.AbsoluteExpiration!.Value.TotalMinutes.Should().BeApproximately(30, 0.1);
        }

        [Fact]
        public void CreateOptions_WithRandomOffset_AddsOffset()
        {
            // Arrange
            var baseExpiration = TimeSpan.FromMinutes(30);
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithExpiration(baseExpiration)
                .WithMaxRandomOffset(300) // 5 minutes max offset
                .Build();
            var context = TestHelpers.CreateScopeContext();

            // Act
            var options1 = definition.CreateOptions(context);
            var options2 = definition.CreateOptions(context);

            // Assert
            options1.AbsoluteExpiration.Should().NotBeNull();
            options2.AbsoluteExpiration.Should().NotBeNull();
            
            // The expiration should be >= base and <= base + maxOffset
            options1.AbsoluteExpiration!.Value.Should().BeGreaterOrEqualTo(baseExpiration);
            options1.AbsoluteExpiration!.Value.Should().BeLessThanOrEqualTo(baseExpiration + TimeSpan.FromSeconds(300));
        }

        [Fact]
        public void CreateOptions_WithTagGenerator_GeneratesTags()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithTagGenerator((context, instance) =>
                {
                    var tags = new[] { "product" };
                    if (instance != null)
                        tags = tags.Append($"product:{instance}").ToArray();
                    return tags;
                })
                .Build();
            var context = TestHelpers.CreateScopeContext();

            // Act
            var options = definition.CreateOptions(context, 123);

            // Assert
            options.Tags.Should().NotBeNull();
            options.Tags.Should().Contain("product");
            options.Tags.Should().Contain("product:123");
        }

        [Fact]
        public void CreateOptions_WithoutTagGenerator_ReturnsEmptyTags()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("product:{id}")
                .Build();
            var context = TestHelpers.CreateScopeContext();

            // Act
            var options = definition.CreateOptions(context);

            // Assert
            options.Tags.Should().NotBeNull();
            options.Tags.Should().BeEmpty();
        }

        [Fact]
        public void CreateOptions_WithNullContext_HandlesGracefully()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithTagGenerator((context, instance) => new[] { "product" })
                .Build();

            // Act
            var options = definition.CreateOptions(null);

            // Assert
            options.Should().NotBeNull();
            options.Tags.Should().BeEmpty(); // TagGenerator not called without context
        }

        #endregion

        #region Scope Configuration Tests

        [Theory]
        [InlineData(CacheScope.Global)]
        [InlineData(CacheScope.Tenant)]
        [InlineData(CacheScope.Store)]
        [InlineData(CacheScope.User)]
        public void WithScope_SetsScopeCorrectly(CacheScope scope)
        {
            // Act
            var definition = CacheKeyDefinition.Create("test-key")
                .WithScope(scope)
                .Build();

            // Assert
            definition.Scope.Should().Be(scope);
        }

        #endregion

        #region Expiration Configuration Tests

        [Fact]
        public void WithExpiration_SetsExpirationCorrectly()
        {
            // Arrange
            var expiration = TimeSpan.FromHours(2);

            // Act
            var definition = CacheKeyDefinition.Create("test-key")
                .WithExpiration(expiration)
                .Build();

            // Assert
            definition.DefaultExpiration.Should().Be(expiration);
        }

        #endregion

        #region AllowNull Configuration Tests

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AllowNull_SetsValueCorrectly(bool allowNull)
        {
            // Act
            var definition = CacheKeyDefinition.Create("test-key")
                .AllowNull(allowNull)
                .Build();

            // Assert
            definition.AllowNull.Should().Be(allowNull);
        }

        #endregion

        #region EnableL1Cache Configuration Tests

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EnableL1Cache_SetsValueCorrectly(bool enable)
        {
            // Act
            var definition = CacheKeyDefinition.Create("test-key")
                .EnableL1Cache(enable)
                .Build();

            // Assert
            definition.EnableL1Cache.Should().Be(enable);
        }

        #endregion

        #region Description Configuration Tests

        [Fact]
        public void WithDescription_SetsDescriptionCorrectly()
        {
            // Arrange
            var description = "Test cache key for products";

            // Act
            var definition = CacheKeyDefinition.Create("test-key")
                .WithDescription(description)
                .Build();

            // Assert
            definition.Description.Should().Be(description);
        }

        #endregion
    }
}
