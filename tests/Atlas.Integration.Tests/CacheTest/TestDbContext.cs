// Infrastructure/TestDbContext.cs
using Microsoft.EntityFrameworkCore;

namespace Atlas.Integration.Tests.Infrastructure
{
    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // MySQL 5.6 兼容配置
            modelBuilder.HasCharSet("utf8mb4"); // 使用 utf8mb4

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd(); // 明确指定自增

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasCharSet("utf8mb4")
                    .UseCollation("utf8mb4_unicode_ci");

                entity.Property(e => e.TenantId)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasCharSet("utf8mb4");

                entity.Property(e => e.Description)
                    .HasMaxLength(1000)
                    .HasCharSet("utf8mb4");

                entity.Property(e => e.Price)
                    .HasColumnType("decimal(18,2)");

                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => new { e.TenantId, e.CategoryId });

                // MySQL 5.6 索引长度限制
                entity.HasIndex(e => e.Name)
                    .HasDatabaseName("IX_Products_Name");
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(100)
                    .HasCharSet("utf8mb4");

                entity.Property(e => e.TenantId)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasCharSet("utf8mb4");

                entity.Property(e => e.Description)
                    .HasMaxLength(500)
                    .HasCharSet("utf8mb4");

                entity.HasIndex(e => e.TenantId);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.TenantId)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasCharSet("utf8mb4");

                entity.Property(e => e.OrderNumber)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasCharSet("utf8mb4");

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasCharSet("utf8mb4");

                entity.Property(e => e.TotalAmount)
                    .HasColumnType("decimal(18,2)");

                entity.Property(e => e.OrderDate)
                    .HasColumnType("datetime"); // MySQL 5.6 使用 datetime 而不是 datetime2

                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.OrderNumber).IsUnique();
                entity.HasIndex(e => e.CustomerId);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasCharSet("utf8mb4");

                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasCharSet("utf8mb4");

                entity.Property(e => e.TenantId)
                    .IsRequired()
                    .HasMaxLength(50)
                    .HasCharSet("utf8mb4");

                entity.HasIndex(e => e.TenantId);
                entity.HasIndex(e => e.Email);
            });
        }
    }
}