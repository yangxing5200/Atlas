using Atlas.Infrastructure.Caching.Serialization;
using Atlas.Infrastructure.Caching.Tests.Helpers;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Serialization
{
    public class JsonCacheSerializerTests
    {
        private readonly JsonCacheSerializer _serializer;

        public JsonCacheSerializerTests()
        {
            _serializer = new JsonCacheSerializer();
        }

        #region Serialize Tests

        [Fact]
        public void Serialize_WithSimpleObject_SerializesCorrectly()
        {
            // Arrange
            var product = TestDataGenerator.CreateProduct(1, "Test Product");

            // Act
            var data = _serializer.Serialize(product);

            // Assert
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Serialize_WithComplexObject_SerializesCorrectly()
        {
            // Arrange
            var complexObject = new ComplexTestObject
            {
                Id = 1,
                Name = "Test",
                Tags = new List<string> { "tag1", "tag2" },
                Metadata = new Dictionary<string, string>
                {
                    { "key1", "value1" },
                    { "key2", "value2" }
                },
                Nested = new NestedObject
                {
                    Value = "nested-value",
                    Count = 42
                }
            };

            // Act
            var data = _serializer.Serialize(complexObject);

            // Assert
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Serialize_WithNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _serializer.Serialize<TestProduct>(null!));
        }

        [Fact]
        public void Serialize_WithCircularReference_Handles()
        {
            // Arrange
            var parent = new CircularReferenceTest { Id = 1, Name = "Parent" };
            var child = new CircularReferenceTest { Id = 2, Name = "Child", Parent = parent };
            parent.Child = child;

            // Act
            var data = _serializer.Serialize(parent);

            // Assert
            data.Should().NotBeNull();
            // JsonSerializer with ReferenceHandler.Preserve should handle this
        }

        #endregion

        #region Deserialize Tests

        [Fact]
        public void Deserialize_WithValidData_ReturnsCorrectObject()
        {
            // Arrange
            var original = TestDataGenerator.CreateProduct(1, "Test Product");
            var serializedData = _serializer.Serialize(original);

            // Act
            var deserialized = _serializer.Deserialize<TestProduct>(serializedData);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(original.Id);
            deserialized.Name.Should().Be(original.Name);
            deserialized.Price.Should().Be(original.Price);
        }

        [Fact]
        public void Deserialize_WithComplexObject_ReturnsCorrectObject()
        {
            // Arrange
            var original = new ComplexTestObject
            {
                Id = 1,
                Name = "Test",
                Tags = new List<string> { "tag1", "tag2" },
                Metadata = new Dictionary<string, string>
                {
                    { "key1", "value1" },
                    { "key2", "value2" }
                },
                Nested = new NestedObject
                {
                    Value = "nested-value",
                    Count = 42
                }
            };
            var serializedData = _serializer.Serialize(original);

            // Act
            var deserialized = _serializer.Deserialize<ComplexTestObject>(serializedData);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(original.Id);
            deserialized.Name.Should().Be(original.Name);
            deserialized.Tags.Should().BeEquivalentTo(original.Tags);
            deserialized.Metadata.Should().BeEquivalentTo(original.Metadata);
            deserialized.Nested.Should().NotBeNull();
            deserialized.Nested!.Value.Should().Be(original.Nested.Value);
            deserialized.Nested.Count.Should().Be(original.Nested.Count);
        }

        [Fact]
        public void Deserialize_WithNullData_ReturnsDefault()
        {
            // Act
            var result = _serializer.Deserialize<TestProduct>(null!);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Deserialize_WithEmptyData_ReturnsDefault()
        {
            // Arrange
            var emptyData = Array.Empty<byte>();

            // Act
            var result = _serializer.Deserialize<TestProduct>(emptyData);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Deserialize_WithInvalidData_ThrowsException()
        {
            // Arrange
            var invalidData = new byte[] { 1, 2, 3, 4, 5 };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                _serializer.Deserialize<TestProduct>(invalidData));
        }

        #endregion

        #region Roundtrip Tests

        [Fact]
        public void Roundtrip_SimpleObject_PreservesData()
        {
            // Arrange
            var original = TestDataGenerator.CreateProduct(123, "Roundtrip Product");

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<TestProduct>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized!.Id.Should().Be(original.Id);
            deserialized.Name.Should().Be(original.Name);
            deserialized.Price.Should().Be(original.Price);
        }

        [Fact]
        public void Roundtrip_WithListOfObjects_PreservesData()
        {
            // Arrange
            var original = TestDataGenerator.CreateProducts(5);

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<List<TestProduct>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().HaveCount(5);
            deserialized.Should().BeEquivalentTo(original);
        }

        [Fact]
        public void Roundtrip_WithDictionary_PreservesData()
        {
            // Arrange
            var original = new Dictionary<string, TestProduct>
            {
                { "product1", TestDataGenerator.CreateProduct(1) },
                { "product2", TestDataGenerator.CreateProduct(2) },
                { "product3", TestDataGenerator.CreateProduct(3) }
            };

            // Act
            var serialized = _serializer.Serialize(original);
            var deserialized = _serializer.Deserialize<Dictionary<string, TestProduct>>(serialized);

            // Assert
            deserialized.Should().NotBeNull();
            deserialized.Should().HaveCount(3);
            deserialized.Should().ContainKey("product1");
            deserialized.Should().ContainKey("product2");
            deserialized.Should().ContainKey("product3");
        }

        #endregion

        #region SerializerName Tests

        [Fact]
        public void SerializerName_ReturnsJson()
        {
            // Act
            var name = _serializer.SerializerName;

            // Assert
            name.Should().Be("Json");
        }

        #endregion

        #region Test Helper Classes

        private class ComplexTestObject
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public List<string> Tags { get; set; } = new();
            public Dictionary<string, string> Metadata { get; set; } = new();
            public NestedObject? Nested { get; set; }
        }

        private class NestedObject
        {
            public string Value { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        private class CircularReferenceTest
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public CircularReferenceTest? Parent { get; set; }
            public CircularReferenceTest? Child { get; set; }
        }

        #endregion
    }
}
