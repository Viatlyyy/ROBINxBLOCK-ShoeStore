using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Integration;

[TestClass]
[DoNotParallelize]
public class TSortingDatabase
{
    private static TemporaryPostgreSql database = null!;
    private static DbContextOptions<ApplicationDbContext> options = null!;

    [ClassInitialize]
    public static async Task Setup(TestContext context)
    {
        database = await TemporaryPostgreSql.StartAsync();
        try
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ShoeStore.csproj")))
                directory = directory.Parent;
            if (directory is null) throw new DirectoryNotFoundException("Не найден веб-проект.");
            await database.ImportSchemaAsync(Path.Combine(directory.FullName, "Database", "shoestore-schema.sql"));
            options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options);
            db.Brands.Add(new Brand { Id = 1, Name = "Учебный бренд" });
            db.Categories.Add(new Category { Id = 1, Name = "Кроссовки" });
            db.Products.AddRange(
                CreateProduct(1, 20m, false, true, ProductStatus.Active),
                CreateProduct(4, 50m, true, false, ProductStatus.Active),
                CreateProduct(8, 70m, true, true, ProductStatus.Active),
                CreateProduct(5, 5m, true, true, ProductStatus.Active),
                CreateProduct(2, 1m, true, true, ProductStatus.Draft),
                CreateProduct(7, 999m, true, true, ProductStatus.Archived));
            await db.SaveChangesAsync();
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    [ClassCleanup]
    public static async Task Cleanup() => await database.DisposeAsync();

    [TestMethod]
    [TestCategory("Sorting")]
    public async Task GetHomeAsync_PostgreSqlFiltersAndOrdersWithoutChangingData()
    {
        var before = await database.CatalogSnapshotAsync();
        await using var db = new ApplicationDbContext(options);

        var catalog = new CatalogQueryService(db);
        var selections = new[]
        {
            (Tag: "new", Sort: "price-asc", Expected: new[] { 5, 4, 8 }),
            (Tag: "popular", Sort: "price-desc", Expected: new[] { 8, 1, 5 })
        };
        foreach (var selection in selections)
        {
            var model = await catalog.GetHomeAsync(
                new CatalogSelection(selection.Tag, selection.Sort), CancellationToken.None);

            CollectionAssert.AreEqual(selection.Expected,
                model.Products.Select(product => product.Id).ToArray(),
                $"Фильтр {selection.Tag}, сортировка {selection.Sort}");
        }
        Assert.AreEqual(before, await database.CatalogSnapshotAsync());
    }

    private static Product CreateProduct(int id, decimal price, bool isNew, bool isPopular, ProductStatus status) => new()
    {
        Id = id, Name = "Модель " + id, Description = "Описание", BrandId = 1, CategoryId = 1,
        Price = price, IsNew = isNew, IsPopular = isPopular, Status = status, ImageUrl = "/images/products/9060.jpg"
    };
}
