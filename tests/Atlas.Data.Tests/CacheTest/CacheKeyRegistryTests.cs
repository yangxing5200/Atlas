using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Core.Models.Registry;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Registry
{
    public class CacheKeyRegistryTests : IDisposable
    {
        public CacheKeyRegistryTests()
        {
            // Clear registry before each test
            CacheKeyRegistry.Clear();
        }

        public void Dispose()
        {
            // Clear registry after each test
            CacheKeyRegistry.Clear();
        }

        #region Register Tests

        [Fact]
        public void Register_WithValidDefinition_AddsToRegistry()
        {
            // Arrange
            var definition = CacheKeyDefinition
                .Create("test:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            // Act
            CacheKeyRegistry.Register("Test.Definition", definition, "Test");

            // Assert
            var result = CacheKeyRegistry.Get("Test.Definition");
            result.Should().NotBeNull();
            result!.Name.Should().Be("Test.Definition");
            result.Definition.Should().Be(definition);
            result.Category.Should().Be("Test");
        }

        [Fact]
        public void Register_WithDuplicateName_ThrowsArgumentException()
        {
            // Arrange
            var definition1 = CacheKeyDefinition
                .Create("test1:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            var definition2 = CacheKeyDefinition
                .Create("test2:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            CacheKeyRegistry.Register("Test.Definition", definition1, "Test");

            // Act & Assert
            Assert.Throws<ArgumentException>(
                () => CacheKeyRegistry.Register("Test.Definition", definition2, "Test"));
        }

        [Fact]
        public void Register_WithNullName_ThrowsArgumentException()
        {
            // Arrange
            var definition = CacheKeyDefinition
                .Create("test:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            // Act & Assert
            Assert.Throws<ArgumentException>(
                () => CacheKeyRegistry.Register(null!, definition, "Test"));
        }

        [Fact]
        public void Register_WithNullDefinition_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(
                () => CacheKeyRegistry.Register("Test.Definition", null!, "Test"));
        }

        #endregion

        #region Get Tests

        [Fact]
        public void Get_WhenExists_ReturnsDefinition()
        {
            // Arrange
            var definition = CacheKeyDefinition
                .Create("test:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            CacheKeyRegistry.Register("Test.Definition", definition, "Test");

            // Act
            var result = CacheKeyRegistry.Get("Test.Definition");

            // Assert
            result.Should().NotBeNull();
            result!.Definition.Should().Be(definition);
        }

        [Fact]
        public void Get_WhenNotExists_ReturnsNull()
        {
            // Act
            var result = CacheKeyRegistry.Get("NonExistent");

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetAll Tests

        [Fact]
        public void GetAll_ReturnsAllRegistered()
        {
            // Arrange
            var definition1 = CacheKeyDefinition
                .Create("test1:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            var definition2 = CacheKeyDefinition
                .Create("test2:{id}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(60))
                .Build();

            CacheKeyRegistry.Register("Test.Definition1", definition1, "Test");
            CacheKeyRegistry.Register("Test.Definition2", definition2, "Test");

            // Act
            var results = CacheKeyRegistry.GetAll().ToList();

            // Assert
            results.Should().HaveCount(2);
            results.Select(r => r.Name).Should().Contain("Test.Definition1");
            results.Select(r => r.Name).Should().Contain("Test.Definition2");
        }

        #endregion

        #region GetByCategory Tests

        [Fact]
        public void GetByCategory_ReturnsMatchingOnly()
        {
            // Arrange
            var definition1 = CacheKeyDefinition
                .Create("test1:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            var definition2 = CacheKeyDefinition
                .Create("test2:{id}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(60))
                .Build();

            CacheKeyRegistry.Register("Category1.Definition", definition1, "Category1");
            CacheKeyRegistry.Register("Category2.Definition", definition2, "Category2");

            // Act
            var results = CacheKeyRegistry.GetByCategory("Category1").ToList();

            // Assert
            results.Should().HaveCount(1);
            results[0].Name.Should().Be("Category1.Definition");
        }

        [Fact]
        public void GetByCategory_IsCaseInsensitive()
        {
            // Arrange
            var definition = CacheKeyDefinition
                .Create("test:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            CacheKeyRegistry.Register("Test.Definition", definition, "TestCategory");

            // Act
            var results = CacheKeyRegistry.GetByCategory("testcategory").ToList();

            // Assert
            results.Should().HaveCount(1);
        }

        #endregion

        #region ValidateAll Tests

        [Fact]
        public void ValidateAll_WithValidDefinitions_ReturnsTrue()
        {
            // Arrange
            var definition = CacheKeyDefinition
                .Create("test:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            CacheKeyRegistry.Register("Test.Definition", definition, "Test");

            // Act
            var isValid = CacheKeyRegistry.ValidateAll();

            // Assert
            isValid.Should().BeTrue();
            CacheKeyRegistry.GetValidationErrors().Should().BeEmpty();
        }

        [Fact]
        public void ValidateAll_WithDuplicatePatterns_ReturnsFalse()
        {
            // Arrange
            var definition1 = CacheKeyDefinition
                .Create("test:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            var definition2 = CacheKeyDefinition
                .Create("test:{id}")  // Same pattern
                .WithScope(CacheScope.Global)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(60))
                .Build();

            CacheKeyRegistry.Register("Test.Definition1", definition1, "Test");
            CacheKeyRegistry.Register("Test.Definition2", definition2, "Test");

            // Act
            var isValid = CacheKeyRegistry.ValidateAll();

            // Assert
            isValid.Should().BeFalse();
            CacheKeyRegistry.GetValidationErrors().Should().NotBeEmpty();
        }

        #endregion

        #region Clear Tests

        [Fact]
        public void Clear_RemovesAllDefinitions()
        {
            // Arrange
            var definition = CacheKeyDefinition
                .Create("test:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .Build();

            CacheKeyRegistry.Register("Test.Definition", definition, "Test");

            // Act
            CacheKeyRegistry.Clear();

            // Assert
            CacheKeyRegistry.GetAll().Should().BeEmpty();
        }

        #endregion
    }
}
