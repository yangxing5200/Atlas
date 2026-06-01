using Atlas.Core.Entities.Interfaces;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Atlas.Data.Tests
{
    public class EntityScopeFilterTests
    {
        #region Test Entities

        /// <summary>
        /// Test entity that implements ITenantEntity only (no store filtering)
        /// </summary>
        private class TenantOnlyEntity : ITenantEntity
        {
            public long Id { get; set; }
            public long TenantId { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// Test entity that implements IStoreOnlyEntity (tenant + store filtering)
        /// </summary>
        private class StoreOnlyTestEntity : IStoreOnlyEntity
        {
            public long Id { get; set; }
            public long TenantId { get; set; }
            public long StoreId { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// Test entity that implements ISharedEntity (tenant + shared store filtering)
        /// </summary>
        private class SharedTestEntity : ISharedEntity
        {
            public long Id { get; set; }
            public long TenantId { get; set; }
            public long StoreId { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// Simple entity without tenant or store interfaces
        /// </summary>
        private class SimpleEntity
        {
            public long Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        #endregion

        #region Helper Methods

        private static Mock<IDataScope> CreateDataScope(long? tenantId = null, long? storeId = null, List<long>? shareStoreIds = null)
        {
            var mockScope = new Mock<IDataScope>();
            mockScope.Setup(s => s.TenantId).Returns(tenantId);
            mockScope.Setup(s => s.StoreId).Returns(storeId);
            mockScope.Setup(s => s.GetShareStoreIds()).Returns(shareStoreIds ?? new List<long>());
            return mockScope;
        }

        #endregion

        #region TenantEntity Tests

        [Fact]
        public void ApplyScope_TenantEntity_WithExplicitTenantId_ShouldUseExplicitValue()
        {
            // Arrange
            var data = new List<TenantOnlyEntity>
            {
                new() { Id = 1, TenantId = 100, Name = "Entity1" },
                new() { Id = 2, TenantId = 200, Name = "Entity2" },
                new() { Id = 3, TenantId = 100, Name = "Entity3" }
            }.AsQueryable();

            // Scope has TenantId = 999 (should be ignored when explicit tenantId is provided)
            var mockScope = CreateDataScope(tenantId: 999);

            // Act - use explicit tenantId = 100
            var result = data.ApplyScope(mockScope.Object, explicitTenantId: 100).ToList();

            // Assert
            result.Should().HaveCount(2);
            result.All(e => e.TenantId == 100).Should().BeTrue();
        }

        [Fact]
        public void ApplyScope_TenantEntity_WithNullScopeTenantId_AndExplicitTenantId_ShouldUseExplicitValue()
        {
            // Arrange - Simulates login scenario where IDataScope.TenantId is null
            var data = new List<TenantOnlyEntity>
            {
                new() { Id = 1, TenantId = 100, Name = "Entity1" },
                new() { Id = 2, TenantId = 200, Name = "Entity2" },
                new() { Id = 3, TenantId = 100, Name = "Entity3" }
            }.AsQueryable();

            // Scope has TenantId = null (simulating login scenario)
            var mockScope = CreateDataScope(tenantId: null);

            // Act - use explicit tenantId = 100
            var result = data.ApplyScope(mockScope.Object, explicitTenantId: 100).ToList();

            // Assert
            result.Should().HaveCount(2);
            result.All(e => e.TenantId == 100).Should().BeTrue();
        }

        [Fact]
        public void ApplyScope_TenantEntity_WithScopeTenantId_NoExplicitTenantId_ShouldUseScopeValue()
        {
            // Arrange
            var data = new List<TenantOnlyEntity>
            {
                new() { Id = 1, TenantId = 100, Name = "Entity1" },
                new() { Id = 2, TenantId = 200, Name = "Entity2" },
                new() { Id = 3, TenantId = 100, Name = "Entity3" }
            }.AsQueryable();

            var mockScope = CreateDataScope(tenantId: 200);

            // Act - no explicit tenantId
            var result = data.ApplyScope(mockScope.Object).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].TenantId.Should().Be(200);
        }

        [Fact]
        public void ApplyScope_TenantEntity_WithNoTenantIdAnywhere_ShouldReturnEmpty()
        {
            // Arrange
            var data = new List<TenantOnlyEntity>
            {
                new() { Id = 1, TenantId = 100, Name = "Entity1" },
                new() { Id = 2, TenantId = 200, Name = "Entity2" }
            }.AsQueryable();

            var mockScope = CreateDataScope(tenantId: null);

            // Act - no explicit tenantId and scope has null
            var result = data.ApplyScope(mockScope.Object).ToList();

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region StoreOnlyEntity Tests

        [Fact]
        public void ApplyScope_StoreOnlyEntity_WithExplicitTenantIdAndStoreId_ShouldUseBothExplicitValues()
        {
            // Arrange
            var data = new List<StoreOnlyTestEntity>
            {
                new() { Id = 1, TenantId = 100, StoreId = 10, Name = "Entity1" },
                new() { Id = 2, TenantId = 100, StoreId = 20, Name = "Entity2" },
                new() { Id = 3, TenantId = 200, StoreId = 10, Name = "Entity3" }
            }.AsQueryable();

            var mockScope = CreateDataScope(tenantId: 999, storeId: 999);

            // Act
            var result = data.ApplyScope(mockScope.Object, explicitTenantId: 100, explicitStoreId: 10).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].TenantId.Should().Be(100);
            result[0].StoreId.Should().Be(10);
        }

        [Fact]
        public void ApplyScope_StoreOnlyEntity_WithExplicitTenantId_NoExplicitStoreId_ShouldUseScopeStoreId()
        {
            // Arrange
            var data = new List<StoreOnlyTestEntity>
            {
                new() { Id = 1, TenantId = 100, StoreId = 10, Name = "Entity1" },
                new() { Id = 2, TenantId = 100, StoreId = 20, Name = "Entity2" }
            }.AsQueryable();

            var mockScope = CreateDataScope(tenantId: null, storeId: 20);

            // Act
            var result = data.ApplyScope(mockScope.Object, explicitTenantId: 100).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].StoreId.Should().Be(20);
        }

        #endregion

        #region SharedEntity Tests

        [Fact]
        public void ApplyScope_SharedEntity_WithExplicitStoreId_ShouldFilterToSingleStore()
        {
            // Arrange
            var data = new List<SharedTestEntity>
            {
                new() { Id = 1, TenantId = 100, StoreId = 10, Name = "Entity1" },
                new() { Id = 2, TenantId = 100, StoreId = 20, Name = "Entity2" },
                new() { Id = 3, TenantId = 100, StoreId = 30, Name = "Entity3" }
            }.AsQueryable();

            // Even though scope has multiple share store ids, explicit storeId should override
            var mockScope = CreateDataScope(tenantId: 100, storeId: 10, shareStoreIds: new List<long> { 10, 20, 30 });

            // Act - explicit storeId should filter to single store
            var result = data.ApplyScope(mockScope.Object, explicitTenantId: 100, explicitStoreId: 20).ToList();

            // Assert
            result.Should().HaveCount(1);
            result[0].StoreId.Should().Be(20);
        }

        [Fact]
        public void ApplyScope_SharedEntity_WithNoExplicitStoreId_ShouldUseShareStoreIds()
        {
            // Arrange
            var data = new List<SharedTestEntity>
            {
                new() { Id = 1, TenantId = 100, StoreId = 10, Name = "Entity1" },
                new() { Id = 2, TenantId = 100, StoreId = 20, Name = "Entity2" },
                new() { Id = 3, TenantId = 100, StoreId = 30, Name = "Entity3" }
            }.AsQueryable();

            var mockScope = CreateDataScope(tenantId: 100, storeId: 10, shareStoreIds: new List<long> { 10, 20 });

            // Act - no explicit storeId, should use share store ids
            var result = data.ApplyScope(mockScope.Object).ToList();

            // Assert
            result.Should().HaveCount(2);
            result.Select(e => e.StoreId).Should().BeEquivalentTo(new[] { 10L, 20L });
        }

        [Fact]
        public void ApplyScope_SharedEntity_WithResolvedSnapshot_ShouldUseSnapshotShareStoreIds()
        {
            // Arrange
            var data = new List<SharedTestEntity>
            {
                new() { Id = 1, TenantId = 100, StoreId = 10, Name = "Entity1" },
                new() { Id = 2, TenantId = 100, StoreId = 20, Name = "Entity2" },
                new() { Id = 3, TenantId = 100, StoreId = 30, Name = "Entity3" },
                new() { Id = 4, TenantId = 200, StoreId = 10, Name = "Entity4" }
            }.AsQueryable();

            var snapshot = new DataScopeSnapshot(
                TenantId: 100,
                StoreId: 10,
                ShareStoreIds: new List<long> { 10, 20 });

            // Act
            var result = data.ApplyScope(snapshot).ToList();

            // Assert
            result.Should().HaveCount(2);
            result.Select(e => e.Id).Should().BeEquivalentTo(new[] { 1L, 2L });
        }

        #endregion

        #region SimpleEntity Tests (No Tenant/Store Filtering)

        [Fact]
        public void ApplyScope_SimpleEntity_ShouldReturnAllRecords()
        {
            // Arrange
            var data = new List<SimpleEntity>
            {
                new() { Id = 1, Name = "Entity1" },
                new() { Id = 2, Name = "Entity2" }
            }.AsQueryable();

            var mockScope = CreateDataScope(tenantId: null, storeId: null);

            // Act
            var result = data.ApplyScope(mockScope.Object).ToList();

            // Assert
            result.Should().HaveCount(2);
        }

        #endregion

        #region Null Parameter Tests

        [Fact]
        public void ApplyScope_WithNullQuery_ShouldThrowArgumentNullException()
        {
            // Arrange
            IQueryable<TenantOnlyEntity>? query = null;
            var mockScope = CreateDataScope(tenantId: 100);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => query!.ApplyScope(mockScope.Object));
        }

        [Fact]
        public void ApplyScope_WithNullScope_ShouldThrowArgumentNullException()
        {
            // Arrange
            var data = new List<TenantOnlyEntity>
            {
                new() { Id = 1, TenantId = 100, Name = "Entity1" }
            }.AsQueryable();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => data.ApplyScope((IDataScope)null!));
        }

        #endregion

        #region Login Scenario Integration Tests

        [Fact]
        public void ApplyScope_LoginScenario_WithNullScopeTenantId_ExplicitTenantId_ShouldWork()
        {
            // Arrange - This simulates the exact login scenario from the issue
            var data = new List<TenantOnlyEntity>
            {
                new() { Id = 1, TenantId = 100, Name = "User1" },
                new() { Id = 2, TenantId = 100, Name = "User2" },
                new() { Id = 3, TenantId = 200, Name = "User3" }
            }.AsQueryable();

            // During login, ICurrentIdentity.TenantId is null, so IDataScope.TenantId is also null
            var mockScope = CreateDataScope(tenantId: null, storeId: null);

            // Act - QueryAsync(long tenantId) should pass explicit tenantId
            var result = data.ApplyScope(mockScope.Object, explicitTenantId: 100).ToList();

            // Assert - Should correctly filter by the explicit tenantId
            result.Should().HaveCount(2);
            result.All(e => e.TenantId == 100).Should().BeTrue();
        }

        #endregion
    }
}
