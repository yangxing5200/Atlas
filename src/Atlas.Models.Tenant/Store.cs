using Atlas.Core.Entities;

namespace Atlas.Models.Tenant
{
    public class Store: AuditableEntity
    {
        public required string Name { get; set; }
    }
}
