// EntityFramework/ChangeTracking/EntityChange.cs
using System;
using System.Collections.Generic;

namespace Atlas.Infrastructure.Caching.EntityFramework.ChangeTracking
{
    public class EntityChange
    {
        public Type EntityType { get; set; } = null!;
        public string EntityName { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public ChangeType ChangeType { get; set; }
        public object Entity { get; set; } = null!;
        public HashSet<string> ModifiedProperties { get; set; } = new();
    }
}