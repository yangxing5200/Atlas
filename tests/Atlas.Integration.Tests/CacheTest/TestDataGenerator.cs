// Fixtures/TestDataGenerator.cs
using Bogus;

namespace Atlas.Integration.Tests.Fixtures
{
    public static class TestDataGenerator
    {
        public static List<Product> GenerateProducts(int count, string tenantId, int? categoryId = null)
        {
            var faker = new Faker<Product>()
                .RuleFor(p => p.Name, f => f.Commerce.ProductName())
                .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
                .RuleFor(p => p.Price, f => f.Random.Decimal(10, 1000))
                .RuleFor(p => p.Stock, f => f.Random.Int(0, 100))
                .RuleFor(p => p.TenantId, _ => tenantId)
                .RuleFor(p => p.CategoryId, _ => categoryId)
                .RuleFor(p => p.CreatedAt, f => f.Date.Recent(30));

            return faker.Generate(count);
        }

        public static List<Category> GenerateCategories(int count, string tenantId)
        {
            var faker = new Faker<Category>()
                .RuleFor(c => c.Name, f => f.Commerce.Categories(1)[0])
                .RuleFor(c => c.Description, f => f.Lorem.Sentence())
                .RuleFor(c => c.TenantId, _ => tenantId);

            return faker.Generate(count);
        }

        public static List<Customer> GenerateCustomers(int count, string tenantId)
        {
            var faker = new Faker<Customer>()
                .RuleFor(c => c.Name, f => f.Name.FullName())
                .RuleFor(c => c.Email, f => f.Internet.Email())
                .RuleFor(c => c.TenantId, _ => tenantId);

            return faker.Generate(count);
        }

        public static List<Order> GenerateOrders(int count, string tenantId, List<Customer> customers)
        {
            var faker = new Faker<Order>()
                .RuleFor(o => o.OrderNumber, f => f.Random.AlphaNumeric(10).ToUpper())
                .RuleFor(o => o.TenantId, _ => tenantId)
                .RuleFor(o => o.CustomerId, f => f.PickRandom(customers).Id)
                .RuleFor(o => o.TotalAmount, f => f.Random.Decimal(50, 5000))
                .RuleFor(o => o.OrderDate, f => f.Date.Recent(60))
                .RuleFor(o => o.Status, f => f.PickRandom("Pending", "Processing", "Completed", "Cancelled"));

            return faker.Generate(count);
        }
    }
}