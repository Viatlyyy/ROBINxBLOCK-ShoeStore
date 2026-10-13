using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Models;

namespace ShoeStore.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantImage> ProductVariantImages => Set<ProductVariantImage>();
    public DbSet<ProductVariantSize> ProductVariantSizes => Set<ProductVariantSize>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.HasPostgresExtension("citext");
        b.HasPostgresExtension("pg_trgm");
        b.Entity<Brand>().Property(x => x.Name).HasColumnType("citext");
        b.Entity<Brand>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Brand>().HasIndex(x => x.Name, "IX_Brands_Name_Trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        b.Entity<Category>().Property(x => x.Name).HasColumnType("citext");
        b.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Product>().Property(x => x.Price).HasPrecision(10, 2);
        b.Entity<Product>().Property(x => x.OldPrice).HasPrecision(10, 2);
        b.Entity<Product>().Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Entity<Product>().HasIndex(x => x.Status);
        b.Entity<Product>().HasIndex(x => new { x.Status, x.BrandId });
        b.Entity<Product>().HasIndex(x => new { x.Status, x.CategoryId });
        b.Entity<Product>().HasIndex(x => new { x.Status, x.IsPopular, x.IsNew });
        b.Entity<Product>().HasIndex(x => x.Name, "IX_Products_Name_Trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        b.Entity<Product>().ToTable(table =>
        {
            table.HasCheckConstraint("CK_Products_Price_Positive", "\"Price\" > 0");
            table.HasCheckConstraint("CK_Products_OldPrice", "\"OldPrice\" IS NULL OR \"OldPrice\" > \"Price\"");
        });
        b.Entity<ProductVariant>().Property(x => x.Name).HasColumnType("citext");
        b.Entity<ProductVariant>().Property(x => x.Sku).HasColumnType("citext");
        b.Entity<ProductVariant>().HasIndex(x => x.Sku).IsUnique();
        b.Entity<ProductVariant>().HasIndex(x => x.Sku, "IX_ProductVariants_Sku_Trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        b.Entity<ProductVariant>().HasIndex(x => new { x.ProductId, x.Name }).IsUnique();
        b.Entity<ProductVariantImage>().HasIndex(x => new { x.ProductVariantId, x.SortOrder }).IsUnique();
        b.Entity<ProductVariantSize>().HasIndex(x => new { x.ProductVariantId, x.Size }).IsUnique();
        b.Entity<ProductVariantSize>().ToTable(table =>
        {
            table.HasCheckConstraint("CK_ProductVariantSizes_Size", "\"Size\" BETWEEN 30 AND 50");
            table.HasCheckConstraint("CK_ProductVariantSizes_Stock", "\"StockQuantity\" BETWEEN 0 AND 999");
        });
    }
}
