using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Integration;

[TestClass]
public class TCatalogQueryService
{
    [TestMethod]
    [TestCategory("CatalogTiles")]
    public async Task GetHomeAsync_ReturnsAllPublishedProductsWithoutChangingDatabase()
    {
        await using var database = await TemporaryPostgreSql.StartAsync();
        await database.ImportSchemaAsync(Path.Combine(FindProjectRoot(), "Database", "shoestore-schema.sql"));
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        db.Brands.Add(new Brand { Id = 1, Name = "Учебный бренд" });
        db.Categories.Add(new Category { Id = 1, Name = "Кроссовки" });
        db.Products.AddRange(Enumerable.Range(1, 18).Select(id => new Product
        {
            Id = id,
            Name = "Товар " + id,
            Description = "Описание из PostgreSQL",
            BrandId = 1,
            CategoryId = 1,
            Price = 10000m + id * 100m + 0.30m,
            ImageUrl = "/images/products/9060.jpg",
            Status = ProductStatus.Active,
            IsNew = id > 16,
            IsPopular = id % 3 == 0
        }));
        db.Products.AddRange(
            new Product { Id = 100, Name = "Черновик", BrandId = 1, CategoryId = 1, Price = 1m, Status = ProductStatus.Draft, IsPopular = true },
            new Product { Id = 101, Name = "Архив", BrandId = 1, CategoryId = 1, Price = 1m, Status = ProductStatus.Archived, IsPopular = true });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var before = await database.CatalogSnapshotAsync();

        var result = await new CatalogQueryService(db).GetHomeAsync(CancellationToken.None);

        Assert.HasCount(18, result.Products);
        CollectionAssert.AreEquivalent(Enumerable.Range(1, 18).ToArray(), result.Products.Select(product => product.Id).ToArray());
        var product = result.Products.Single(item => item.Id == 17);
        Assert.AreEqual("Товар 17", product.Name);
        Assert.AreEqual("Учебный бренд", product.BrandName);
        Assert.AreEqual(11700.30m, product.Price);
        Assert.AreEqual("/images/products/9060.jpg", product.ImageUrl);
        Assert.AreEqual(before, await database.CatalogSnapshotAsync());
    }

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ShoeStore.csproj"))) return directory.FullName;
        throw new DirectoryNotFoundException("Не найден веб-проект ShoeStore.");
    }
}
