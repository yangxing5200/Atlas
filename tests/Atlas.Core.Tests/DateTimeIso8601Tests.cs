using Xunit;
using System.Text.Json;
using Atlas.Core.Extensions;
using Atlas.Core.Converter;

namespace Atlas.Core.Tests
{
    public class DateTimeIso8601Tests
    {
        #region ToIso8601String Extension Method Tests

        [Fact]
        public void ToIso8601String_ShouldReturnCorrectFormat()
        {
            // Arrange
            var utcDateTime = new DateTime(2025, 12, 2, 10, 30, 0, 123, DateTimeKind.Utc);

            // Act
            var result = utcDateTime.ToIso8601String();

            // Assert
            Assert.Equal("2025-12-02T10:30:00.123Z", result);
        }

        [Fact]
        public void ToIso8601String_LocalTime_ShouldConvertToUtc()
        {
            // Arrange
            var localDateTime = new DateTime(2025, 12, 2, 10, 30, 0, 0, DateTimeKind.Local);
            var expectedUtc = localDateTime.ToUniversalTime();

            // Act
            var result = localDateTime.ToIso8601String();

            // Assert
            Assert.EndsWith("Z", result);
            Assert.Contains(expectedUtc.ToString("yyyy-MM-dd"), result);
        }

        [Fact]
        public void ToIso8601String_ShouldIncludeMilliseconds()
        {
            // Arrange
            var dateTime = new DateTime(2025, 12, 2, 10, 30, 0, 456, DateTimeKind.Utc);

            // Act
            var result = dateTime.ToIso8601String();

            // Assert
            Assert.Contains(".456", result);
        }

        [Fact]
        public void ToIso8601String_MinValue_ShouldReturnValidFormat()
        {
            // Arrange
            var dateTime = DateTime.MinValue;

            // Act
            var result = dateTime.ToIso8601String();

            // Assert
            Assert.EndsWith("Z", result);
            Assert.Contains("T", result);
        }

        #endregion

        #region ToIso8601OffsetString Extension Method Tests

        [Fact]
        public void ToIso8601OffsetString_ShouldReturnCorrectFormat()
        {
            // Arrange
            var dateTimeOffset = new DateTimeOffset(2025, 12, 2, 18, 30, 0, 0, TimeSpan.FromHours(8));

            // Act
            var result = dateTimeOffset.ToIso8601OffsetString();

            // Assert
            Assert.Equal("2025-12-02T18:30:00.000+08:00", result);
        }

        [Fact]
        public void ToIso8601OffsetString_UtcOffset_ShouldReturn00_00()
        {
            // Arrange
            var dateTimeOffset = new DateTimeOffset(2025, 12, 2, 10, 30, 0, 0, TimeSpan.Zero);

            // Act
            var result = dateTimeOffset.ToIso8601OffsetString();

            // Assert
            Assert.Equal("2025-12-02T10:30:00.000+00:00", result);
        }

        [Fact]
        public void ToIso8601OffsetString_NegativeOffset_ShouldReturnNegativeOffset()
        {
            // Arrange
            var dateTimeOffset = new DateTimeOffset(2025, 12, 2, 10, 30, 0, 500, TimeSpan.FromHours(-5));

            // Act
            var result = dateTimeOffset.ToIso8601OffsetString();

            // Assert
            Assert.Equal("2025-12-02T10:30:00.500-05:00", result);
        }

        #endregion

        #region Iso8601DateTimeConverter Tests

        [Fact]
        public void Iso8601DateTimeConverter_Serialize_ShouldReturnIso8601Format()
        {
            // Arrange
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new Iso8601DateTimeConverter());
            var testObj = new TestDateTimeClass
            {
                CreatedAt = new DateTime(2025, 12, 2, 10, 30, 0, 123, DateTimeKind.Utc)
            };

            // Act
            var json = JsonSerializer.Serialize(testObj, options);

            // Assert
            Assert.Contains("\"createdAt\":\"2025-12-02T10:30:00.123Z\"", json);
        }

        [Fact]
        public void Iso8601DateTimeConverter_Deserialize_ShouldParseIso8601Format()
        {
            // Arrange
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new Iso8601DateTimeConverter());
            var json = "{\"createdAt\":\"2025-12-02T10:30:00.123Z\"}";

            // Act
            var result = JsonSerializer.Deserialize<TestDateTimeClass>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2025, result.CreatedAt.Year);
            Assert.Equal(12, result.CreatedAt.Month);
            Assert.Equal(2, result.CreatedAt.Day);
            Assert.Equal(10, result.CreatedAt.Hour);
            Assert.Equal(30, result.CreatedAt.Minute);
        }

        [Fact]
        public void Iso8601DateTimeConverter_Serialize_LocalTime_ShouldConvertToUtc()
        {
            // Arrange
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new Iso8601DateTimeConverter());
            var localTime = new DateTime(2025, 12, 2, 10, 30, 0, 0, DateTimeKind.Local);
            var testObj = new TestDateTimeClass { CreatedAt = localTime };

            // Act
            var json = JsonSerializer.Serialize(testObj, options);

            // Assert
            Assert.Contains("Z\"", json);
        }

        #endregion

        #region NullableIso8601DateTimeConverter Tests

        [Fact]
        public void NullableIso8601DateTimeConverter_Serialize_WithValue_ShouldReturnIso8601Format()
        {
            // Arrange
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new NullableIso8601DateTimeConverter());
            var testObj = new TestNullableDateTimeClass
            {
                ExpiresAt = new DateTime(2025, 12, 2, 10, 30, 0, 456, DateTimeKind.Utc)
            };

            // Act
            var json = JsonSerializer.Serialize(testObj, options);

            // Assert
            Assert.Contains("\"expiresAt\":\"2025-12-02T10:30:00.456Z\"", json);
        }

        [Fact]
        public void NullableIso8601DateTimeConverter_Serialize_NullValue_ShouldReturnNull()
        {
            // Arrange
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            options.Converters.Add(new NullableIso8601DateTimeConverter());
            var testObj = new TestNullableDateTimeClass { ExpiresAt = null };

            // Act
            var json = JsonSerializer.Serialize(testObj, options);

            // Assert
            Assert.Contains("\"expiresAt\":null", json);
        }

        [Fact]
        public void NullableIso8601DateTimeConverter_Deserialize_WithValue_ShouldParseCorrectly()
        {
            // Arrange
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new NullableIso8601DateTimeConverter());
            var json = "{\"expiresAt\":\"2025-12-02T10:30:00.456Z\"}";

            // Act
            var result = JsonSerializer.Deserialize<TestNullableDateTimeClass>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ExpiresAt);
            Assert.Equal(2025, result.ExpiresAt.Value.Year);
            Assert.Equal(12, result.ExpiresAt.Value.Month);
            Assert.Equal(2, result.ExpiresAt.Value.Day);
        }

        [Fact]
        public void NullableIso8601DateTimeConverter_Deserialize_NullValue_ShouldReturnNull()
        {
            // Arrange
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new NullableIso8601DateTimeConverter());
            var json = "{\"expiresAt\":null}";

            // Act
            var result = JsonSerializer.Deserialize<TestNullableDateTimeClass>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.Null(result.ExpiresAt);
        }

        #endregion

        #region Test Helper Classes

        private class TestDateTimeClass
        {
            public DateTime CreatedAt { get; set; }
        }

        private class TestNullableDateTimeClass
        {
            public DateTime? ExpiresAt { get; set; }
        }

        #endregion
    }
}
