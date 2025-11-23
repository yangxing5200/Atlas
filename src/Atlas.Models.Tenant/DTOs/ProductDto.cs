namespace Atlas.Models.DTOs
{
    public class ProductDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
        public string? Description { get; set; }

        public long? SourceStoreId { get; set; }
        public bool IsCustomized { get; set; }
    }
}
