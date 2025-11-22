using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Models.Tenant.Requests
{
    public class CreateProductRequest
    {
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
        public string? Description { get; set; }

        public long? SourceStoreId { get; set; }
        public bool IsCustomized { get; set; }
    }
}
