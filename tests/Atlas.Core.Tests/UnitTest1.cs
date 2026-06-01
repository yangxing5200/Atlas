using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Core.IdGenerators;

namespace Atlas.Core.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void SnowflakeId_ShouldBeUnique()
        {
            // Arrange
            var generator = new SnowflakeIdGenerator(1, 1);
            var ids = new HashSet<long>();

            // Act
            for (int i = 0; i < 10000; i++)
            {
                var id = generator.NextId();
                ids.Add(id);
            }

            // Assert
            Assert.Equal(10000, ids.Count); // ����ID����Ψһ��
        }

        [Fact]
        public void SnowflakeId_ShouldBeIncreasing()
        {
            // Arrange
            var generator = new SnowflakeIdGenerator(1, 1);

            // Act
            var id1 = generator.NextId();
            var id2 = generator.NextId();
            var id3 = generator.NextId();

            // Assert
            Assert.True(id2 > id1);
            Assert.True(id3 > id2);
        }

        [Fact]
        public void SnowflakeId_ConcurrentGeneration_ShouldBeUnique()
        {
            // Arrange
            var generator = new SnowflakeIdGenerator(1, 1);
            var ids = new System.Collections.Concurrent.ConcurrentBag<long>();

            // Act
            Parallel.For(0, 10000, i =>
            {
                ids.Add(generator.NextId());
            });

            // Assert
            var distinctIds = ids.Distinct().ToList();
            Assert.Equal(ids.Count, distinctIds.Count); // �������ɵ�IDҲ��Ψһ��
        }

        [Fact]
        public void SnowflakeId_WithDifferentWorkerIds_ShouldBeUnique()
        {
            // Arrange
            var generator1 = new SnowflakeIdGenerator(1, 1);
            var generator2 = new SnowflakeIdGenerator(2, 1);

            // Act
            var ids = new HashSet<long>();
            for (int i = 0; i < 1000; i++)
            {
                ids.Add(generator1.NextId());
                ids.Add(generator2.NextId());
            }

            // Assert
            Assert.Equal(2000, ids.Count);
        }

        [Fact]
        public void SnowflakeId_ShouldContainCorrectWorkerId()
        {
            // Arrange
            long workerId = 5;
            long datacenterId = 3;
            var generator = new SnowflakeIdGenerator(workerId, datacenterId);

            // Act
            var id = generator.NextId();

            // Assert - ����ID�е�workerId��datacenterId
            // ����ѩ���㷨�ṹ: ʱ���(41λ) + ��������ID(5λ) + ����ID(5λ) + ���к�(12λ)
            long extractedWorkerId = (id >> 12) & 0x1F; // ��ȡworkerId (5λ)
            long extractedDatacenterId = (id >> 17) & 0x1F; // ��ȡdatacenterId (5λ)

            Assert.Equal(workerId, extractedWorkerId);
            Assert.Equal(datacenterId, extractedDatacenterId);
        }

        [Fact]
        public void SnowflakeId_HighThroughput_ShouldNotThrow()
        {
            // Arrange
            var generator = new SnowflakeIdGenerator(1, 1);

            // Act & Assert - ��ͬһ���������ɴ���IDӦ�ò����쳣
            var exception = Record.Exception(() =>
            {
                for (int i = 0; i < 5000; i++)
                {
                    generator.NextId();
                }
            });

            Assert.Null(exception);
        }

        [Fact]
        public void SnowflakeId_InvalidWorkerId_ShouldThrowException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => new SnowflakeIdGenerator(-1, 1));
            Assert.Throws<ArgumentException>(() => new SnowflakeIdGenerator(32, 1)); // �������ֵ
        }

        [Fact]
        public void SnowflakeId_Performance_Test()
        {
            // Arrange
            var generator = new SnowflakeIdGenerator(1, 1);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 100000; i++)
            {
                generator.NextId();
            }
            stopwatch.Stop();

            // Assert - 10���ID����Ӧ���ں���ʱ������ɣ�����С��1�룩
            Assert.True(stopwatch.ElapsedMilliseconds < 1000,
                $"����10���ID��ʱ: {stopwatch.ElapsedMilliseconds}ms");
        }
    }
}