// Exceptions/CacheException.cs
using System;

namespace Atlas.Infrastructure.Caching.Exceptions
{
    public class CacheException : Exception
    {
        public CacheException(string message) : base(message) { }
        public CacheException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class TagNotFoundException : CacheException
    {
        public string TagName { get; }
        public TagNotFoundException(string tagName)
            : base($"Tag '{tagName}' not found")
        {
            TagName = tagName;
        }
    }

    public class InvalidScopeException : CacheException
    {
        public InvalidScopeException(string message) : base(message) { }
    }

    public class CrossTenantAccessException : CacheException
    {
        public string? RequestedTenantId { get; }
        public string? CurrentTenantId { get; }

        public CrossTenantAccessException(string? requestedTenantId, string? currentTenantId)
            : base($"Cross-tenant access denied. Requested: {requestedTenantId}, Current: {currentTenantId}")
        {
            RequestedTenantId = requestedTenantId;
            CurrentTenantId = currentTenantId;
        }
    }

    public class SerializationException : CacheException
    {
        public SerializationException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    public class KeyGenerationException : CacheException
    {
        public KeyGenerationException(string message) : base(message) { }
    }
}