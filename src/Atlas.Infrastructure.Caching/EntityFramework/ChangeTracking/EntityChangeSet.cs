// EntityFramework/ChangeTracking/EntityChangeSet.cs
using System;
using System.Collections.Generic;

namespace Atlas.Infrastructure.Caching.EntityFramework.ChangeTracking
{
    public class EntityChangeSet
    {
        public IList<EntityChange> Changes { get; set; } = new List<EntityChange>();
        public DateTime Timestamp { get; set; }
        public int TotalChanges => Changes.Count;
    }
}