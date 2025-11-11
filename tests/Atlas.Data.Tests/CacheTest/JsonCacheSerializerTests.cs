// Serialization/JsonCacheSerializerTests.cs
using System.Text.Json;
using Atlas.Infrastructure.Caching.Serialization;
using FluentAssertions;
using Xunit;

namespace Atlas.Data.Tests.Serialization
{
    public class JsonCacheSerializerTests
    {
        private readonly JsonCacheSerializer _sut;

        public JsonCacheSerializerTests()
        {
            _sut = new JsonCacheSerializer();
        }

        [Fact]
        public void Serialize_ValidObject_ReturnsBytes()
        {
            // Arrange
            var data = new TestData { Id = 1, Name = "Test" };

            // Act
            var result = _sut.Serialize(data);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        }

        [Fact]
        public void Deserialize_ValidBytes_ReturnsObject()
        {
            // Arrange
            var original = new TestData { Id = 1, Name = "Test" };
            var bytes = _sut.Serialize(original);

            // Act
            var result = _sut.Deserialize<TestData>(bytes);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(original);
        }

        [Fact]
        public void Serialize_Then_Deserialize_PreservesData()
        {
            // Arrange
            var original = new ComplexTestData
            {
                Id = 123,
                Name = "Complex",
                Items = new List<string> { "item1", "item2" },
                Properties = new Dictionary<string, string>
                {
                    ["key1"] = "value1",
                    ["key2"] = "value2"
                },
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var bytes = _sut.Serialize(original);
            var result = _sut.Deserialize<ComplexTestData>(bytes);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(original.Id);
            result.Name.Should().Be(original.Name);
            result.Items.Should().BeEquivalentTo(original.Items);
            result.Properties.Should().BeEquivalentTo(original.Properties);
        }

        [Fact]
        public void SerializerName_ReturnsJson()
        {
            // Act & Assert
            _sut.SerializerName.Should().Be("Json");
        }

        private class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        private class ComplexTestData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public List<string> Items { get; set; } = new();
            public Dictionary<string, string> Properties { get; set; } = new();
            public DateTime CreatedAt { get; set; }
        }
    }
}