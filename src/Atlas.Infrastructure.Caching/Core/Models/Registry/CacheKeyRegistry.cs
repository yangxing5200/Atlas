using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Atlas.Infrastructure.Caching.Core.Models.Registry
{
    /// <summary>
    /// Represents metadata for a cache key definition including validation rules.
    /// </summary>
    public class CacheKeyDefinitionMetadata
    {
        /// <summary>
        /// Gets the unique name of the cache key definition.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the cache key definition.
        /// </summary>
        public CacheKeyDefinition Definition { get; }

        /// <summary>
        /// Gets the category of the cache key (e.g., "Global", "Tenant", "Token").
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the registration timestamp.
        /// </summary>
        public DateTime RegisteredAt { get; }

        public CacheKeyDefinitionMetadata(
            string name,
            CacheKeyDefinition definition,
            string category)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Category = category ?? throw new ArgumentNullException(nameof(category));
            RegisteredAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Central registry for all cache key definitions.
    /// Provides validation, discovery, and management of cache keys across the application.
    /// </summary>
    public static class CacheKeyRegistry
    {
        private static readonly ConcurrentDictionary<string, CacheKeyDefinitionMetadata> _registry = new();
        private static readonly List<string> _validationErrors = new();

        /// <summary>
        /// Registers a cache key definition with the registry.
        /// </summary>
        /// <param name="name">Unique name for the cache key definition.</param>
        /// <param name="definition">The cache key definition.</param>
        /// <param name="category">Category for grouping (e.g., "Global", "Tenant").</param>
        /// <exception cref="ArgumentException">Thrown if a definition with the same name already exists.</exception>
        public static void Register(string name, CacheKeyDefinition definition, string category = "Default")
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name cannot be null or empty.", nameof(name));

            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var metadata = new CacheKeyDefinitionMetadata(name, definition, category);

            if (!_registry.TryAdd(name, metadata))
            {
                throw new ArgumentException($"A cache key definition with name '{name}' is already registered.");
            }
        }

        /// <summary>
        /// Gets a cache key definition by name.
        /// </summary>
        /// <param name="name">The name of the cache key definition.</param>
        /// <returns>The cache key definition metadata, or null if not found.</returns>
        public static CacheKeyDefinitionMetadata? Get(string name)
        {
            _registry.TryGetValue(name, out var metadata);
            return metadata;
        }

        /// <summary>
        /// Gets all registered cache key definitions.
        /// </summary>
        /// <returns>All registered cache key definitions.</returns>
        public static IEnumerable<CacheKeyDefinitionMetadata> GetAll()
        {
            return _registry.Values.ToList();
        }

        /// <summary>
        /// Gets all cache key definitions in a specific category.
        /// </summary>
        /// <param name="category">The category to filter by.</param>
        /// <returns>Cache key definitions in the specified category.</returns>
        public static IEnumerable<CacheKeyDefinitionMetadata> GetByCategory(string category)
        {
            return _registry.Values
                .Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Validates all registered cache key definitions.
        /// Should be called at application startup.
        /// </summary>
        /// <returns>True if all definitions are valid; false otherwise.</returns>
        public static bool ValidateAll()
        {
            _validationErrors.Clear();

            foreach (var kvp in _registry)
            {
                try
                {
                    ValidateDefinition(kvp.Key, kvp.Value.Definition);
                }
                catch (Exception ex)
                {
                    _validationErrors.Add($"Validation failed for '{kvp.Key}': {ex.Message}");
                }
            }

            // Check for duplicate patterns (using Name as the pattern)
            var patterns = _registry.Values
                .GroupBy(m => m.Definition.Name)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var duplicate in patterns)
            {
                var names = string.Join(", ", duplicate.Select(m => m.Name));
                _validationErrors.Add($"Duplicate pattern '{duplicate.Key}' found in: {names}");
            }

            return _validationErrors.Count == 0;
        }

        /// <summary>
        /// Gets validation errors from the last validation run.
        /// </summary>
        /// <returns>List of validation errors.</returns>
        public static IReadOnlyList<string> GetValidationErrors()
        {
            return _validationErrors.AsReadOnly();
        }

        /// <summary>
        /// Clears all registered cache key definitions.
        /// Primarily used for testing.
        /// </summary>
        public static void Clear()
        {
            _registry.Clear();
            _validationErrors.Clear();
        }

        private static void ValidateDefinition(string name, CacheKeyDefinition definition)
        {
            // Use Name property which is the pattern template
            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                throw new InvalidOperationException("Cache key pattern cannot be null or empty.");
            }

            // Use DefaultExpiration property
            if (definition.DefaultExpiration <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("Cache key expiration must be positive.");
            }

            // Validate pattern format
            var pattern = definition.Name;
            if (pattern.Contains("{{") || pattern.Contains("}}"))
            {
                throw new InvalidOperationException("Invalid pattern format. Use single braces for placeholders: {key}");
            }

            // If pattern has placeholders, instance key should be defined
            if (pattern.Contains("{") && pattern.Contains("}"))
            {
                // Use InstanceKeyName property
                if (string.IsNullOrWhiteSpace(definition.InstanceKeyName))
                {
                    throw new InvalidOperationException(
                        "Cache key pattern contains placeholders but no instance key is defined.");
                }
            }
        }
    }
}
