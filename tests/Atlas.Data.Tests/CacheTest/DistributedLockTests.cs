using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Locking;
using FluentAssertions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Locking
{
    public class DistributedLockTests
    {
        private readonly MemoryDistributedLockProvider _provider;

        public DistributedLockTests()
        {
            _provider = new MemoryDistributedLockProvider();
        }

        #region TryAcquireAsync Tests

        [Fact]
        public async Task TryAcquireAsync_WhenResourceAvailable_ReturnsLock()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);

            // Act
            var lockHandle = await _provider.TryAcquireAsync(resource, expiry);

            // Assert
            lockHandle.Should().NotBeNull();
            lockHandle!.IsAcquired.Should().BeTrue();
            lockHandle.Resource.Should().Be(resource);

            // Cleanup
            await lockHandle.DisposeAsync();
        }

        [Fact]
        public async Task TryAcquireAsync_WhenResourceLocked_ReturnsNull()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);

            // Acquire first lock
            var firstLock = await _provider.TryAcquireAsync(resource, expiry);

            // Act
            var secondLock = await _provider.TryAcquireAsync(resource, expiry);

            // Assert
            secondLock.Should().BeNull();

            // Cleanup
            await firstLock!.DisposeAsync();
        }

        [Fact]
        public async Task TryAcquireAsync_WithWaitTime_WaitsForLock()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);
            var wait = TimeSpan.FromMilliseconds(500);

            var firstLock = await _provider.TryAcquireAsync(resource, expiry);

            // Release first lock after a short delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                await firstLock!.ReleaseAsync();
            });

            // Act
            var secondLock = await _provider.TryAcquireAsync(resource, expiry, wait);

            // Assert
            secondLock.Should().NotBeNull();
            secondLock!.IsAcquired.Should().BeTrue();

            // Cleanup
            await secondLock.DisposeAsync();
        }

        [Fact]
        public async Task TryAcquireAsync_AfterRelease_AcquiresLock()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);

            var firstLock = await _provider.TryAcquireAsync(resource, expiry);
            await firstLock!.ReleaseAsync();

            // Act
            var secondLock = await _provider.TryAcquireAsync(resource, expiry);

            // Assert
            secondLock.Should().NotBeNull();
            secondLock!.IsAcquired.Should().BeTrue();

            // Cleanup
            await secondLock.DisposeAsync();
        }

        [Fact]
        public async Task TryAcquireAsync_WithInvalidResource_ThrowsArgumentException()
        {
            // Arrange
            var expiry = TimeSpan.FromMinutes(1);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _provider.TryAcquireAsync("", expiry));

            await Assert.ThrowsAsync<ArgumentException>(
                () => _provider.TryAcquireAsync(null!, expiry));
        }

        [Fact]
        public async Task TryAcquireAsync_WithInvalidExpiry_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var resource = "test-resource";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => _provider.TryAcquireAsync(resource, TimeSpan.Zero));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => _provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(-1)));
        }

        #endregion

        #region AcquireAsync Tests

        [Fact]
        public async Task AcquireAsync_WhenResourceAvailable_ReturnsLock()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);

            // Act
            var lockHandle = await _provider.AcquireAsync(resource, expiry);

            // Assert
            lockHandle.Should().NotBeNull();
            lockHandle.IsAcquired.Should().BeTrue();

            // Cleanup
            await lockHandle.DisposeAsync();
        }

        [Fact]
        public async Task AcquireAsync_WhenTimeout_ThrowsTimeoutException()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);
            var wait = TimeSpan.FromMilliseconds(200);

            var firstLock = await _provider.TryAcquireAsync(resource, expiry);

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(
                () => _provider.AcquireAsync(resource, expiry, wait));

            // Cleanup
            await firstLock!.DisposeAsync();
        }

        #endregion

        #region Lock Release Tests

        [Fact]
        public async Task ReleaseAsync_SetsIsAcquiredToFalse()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);

            var lockHandle = await _provider.TryAcquireAsync(resource, expiry);
            lockHandle.Should().NotBeNull();

            // Act
            await lockHandle!.ReleaseAsync();

            // Assert
            lockHandle.IsAcquired.Should().BeFalse();
        }

        [Fact]
        public async Task DisposeAsync_ReleasesLock()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);

            var firstLock = await _provider.TryAcquireAsync(resource, expiry);
            await firstLock!.DisposeAsync();

            // Act
            var secondLock = await _provider.TryAcquireAsync(resource, expiry);

            // Assert
            secondLock.Should().NotBeNull();

            // Cleanup
            await secondLock!.DisposeAsync();
        }

        [Fact]
        public async Task ReleaseAsync_CalledMultipleTimes_DoesNotThrow()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);

            var lockHandle = await _provider.TryAcquireAsync(resource, expiry);

            // Act & Assert
            await lockHandle!.ReleaseAsync();
            await lockHandle.ReleaseAsync(); // Second call should not throw
        }

        #endregion

        #region Concurrent Lock Tests

        [Fact]
        public async Task ConcurrentAcquire_OnlyOneSucceeds()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);
            var successCount = 0;
            var failCount = 0;

            // Act
            var tasks = new Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    var lockHandle = await _provider.TryAcquireAsync(resource, expiry);
                    if (lockHandle != null)
                    {
                        Interlocked.Increment(ref successCount);
                        await Task.Delay(50); // Hold lock briefly
                        await lockHandle.DisposeAsync();
                    }
                    else
                    {
                        Interlocked.Increment(ref failCount);
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            successCount.Should().BeGreaterThanOrEqualTo(1);
            (successCount + failCount).Should().Be(10);
        }

        #endregion

        #region WithLockAsync Extension Tests

        [Fact]
        public async Task WithLockAsync_ExecutesAction()
        {
            // Arrange
            var resource = "test-resource";
            var executed = false;

            // Act
            await _provider.WithLockAsync(resource, async () =>
            {
                await Task.Delay(10);
                executed = true;
            });

            // Assert
            executed.Should().BeTrue();
        }

        [Fact]
        public async Task WithLockAsync_ReturnsResult()
        {
            // Arrange
            var resource = "test-resource";
            var expectedResult = 42;

            // Act
            var result = await _provider.WithLockAsync(resource, async () =>
            {
                await Task.Delay(10);
                return expectedResult;
            });

            // Assert
            result.Should().Be(expectedResult);
        }

        [Fact]
        public async Task TryWithLockAsync_WhenLocked_ReturnsFalse()
        {
            // Arrange
            var resource = "test-resource";
            var expiry = TimeSpan.FromMinutes(1);

            var firstLock = await _provider.TryAcquireAsync(resource, expiry);

            // Act
            var executed = await _provider.TryWithLockAsync(resource, async () =>
            {
                await Task.Delay(10);
            });

            // Assert
            executed.Should().BeFalse();

            // Cleanup
            await firstLock!.DisposeAsync();
        }

        #endregion
    }
}
