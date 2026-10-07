using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Tests.Integration;

internal static class CatalogFixture
{
    // Независимый список макета, не результат HomePageDemoData.Create().
    internal static readonly (int Id, string Brand, string Name, string File)[] Models =
    [
        (1, "New Balance", "9060", "9060"), (2, "New Balance", "1906R", "1906r"),
        (3, "New Balance", "2002R", "2002r"), (8, "Balenciaga", "Track LED", "track-led"),
        (16, "Adidas", "Campus 00s", "campus-00s"), (19, "ASICS", "Gel-Kayano 14", "gel-kayano-14"),
        (22, "Jordan", "Air Jordan 1 Low", "air-jordan-1-low"), (25, "Salomon", "XT-6", "xt-6"),
        (4, "New Balance", "530", "530"), (7, "Balenciaga", "3XL", "3xl"),
        (9, "Balenciaga", "Triple S", "triple-s"), (11, "Nike", "Dunk Low", "dunk-low"),
        (13, "Nike", "Air Force 1", "air-force-1"), (15, "Adidas", "Samba OG", "samba-og"),
        (18, "Adidas", "Yeezy Foam Runner", "yeezy-foam-runner"), (23, "Jordan", "Air Jordan 4", "air-jordan-4")
    ];

    internal static ApplicationDbContext Open(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);

    internal static async Task SeedAsync(string connectionString)
    {
        await using var db = Open(connectionString);
        db.Roles.Add(new IdentityRole { Id = "test-customer", Name = "Customer", NormalizedName = "CUSTOMER" });
        var brands = Models.Select(model => model.Brand).Distinct()
            .Select((name, index) => new Brand { Id = index + 1, Name = name }).ToArray();
        db.Brands.AddRange(brands);
        db.Categories.Add(new Category { Id = 1, Name = "Кроссовки" });
        foreach (var model in Models)
        {
            var variant = new ProductVariant
            {
                Id = model.Id, Name = "Основная расцветка " + model.Id, Sku = "CARD-" + model.Id,
                Swatch = "#ffffff", ImageUrl = $"/images/products/{model.File}.jpg", IsDefault = model.Id != 1,
                Sizes = [new ProductVariantSize { Size = 38, StockQuantity = model.Id == 2 ? 0 : 5 }]
            };
            if (model.Id == 3) variant.Sizes.Clear(); // Вообще нет размеров, а не только нулевой остаток.
            if (model.Id == 1)
                variant.Sizes.AddRange([new() { Size = 39, StockQuantity = 0 }, new() { Size = 40, StockQuantity = 1 }]);
            db.Products.Add(new Product
            {
                Id = model.Id, Name = model.Name, BrandId = brands.Single(brand => brand.Name == model.Brand).Id,
                CategoryId = 1, Status = ProductStatus.Active, ImageUrl = variant.ImageUrl,
                // Отличаются от плиток макета: захардкоженная карточка не пройдёт проверку.
                Price = 30000m + model.Id, Description = $"Описание из PostgreSQL для {model.Name}: <детали> & качество.",
                Variants = model.Id != 1 ? [variant] :
                [
                    variant,
                    new ProductVariant
                    {
                        Id = 29, Name = "Black / Castlerock", Sku = "CARD-29", Swatch = "#25272b", IsDefault = true,
                        ImageUrl = "/images/gallery/optimized/colors/9060-black-castlerock-side.jpg",
                        Sizes = [new() { Size = 41, StockQuantity = 2 }, new() { Size = 42, StockQuantity = 0 }],
                        // Обратный порядок записи отличает сортировку от случайного порядка в БД.
                        GalleryImages =
                        [
                            new() { ImageUrl = "/images/gallery/optimized/colors/9060-black-castlerock-rear.jpg", SortOrder = 10 },
                            new() { ImageUrl = "/images/gallery/optimized/colors/9060-black-castlerock-front.jpg", SortOrder = 5 }
                        ]
                    }
                ]
            });
        }
        db.Products.AddRange(
            new Product { Id = 10001, Name = "Черновик", Description = "Скрыт", Price = 1000m, BrandId = 1, CategoryId = 1, Status = ProductStatus.Draft },
            new Product { Id = 10002, Name = "Архив", Description = "Скрыт", Price = 1000m, BrandId = 1, CategoryId = 1, Status = ProductStatus.Archived });
        await db.SaveChangesAsync();
    }
}
