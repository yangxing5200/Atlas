// Utilities/CacheKeyHelper.cs
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Caching.Utilities
{
    public static class CacheKeyHelper
    {
        public static string GenerateHash(params object[] values)
        {
            var combined = string.Join(":", values.Select(v => v?.ToString() ?? "null"));
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(combined);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        public static string Sanitize(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            // Remove invalid characters
            var invalidChars = new[] { ' ', '\t', '\n', '\r', '{', '}', '[', ']', '(', ')' };
            return string.Concat(key.Where(c => !invalidChars.Contains(c)));
        }

        public static string BuildCompositeKey(params string[] parts)
        {
            return string.Join(":", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }
}