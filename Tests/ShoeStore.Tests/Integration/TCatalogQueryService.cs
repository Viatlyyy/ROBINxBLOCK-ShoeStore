using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
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
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = configuration["SHOESTORE_TEST_CONNECTION_STRING"]
            ?? configuration.GetConnectionString("DefaultConnection");
        Assert.IsFalse(string.IsNullOrWhiteSpace(connectionString),
            "Настройте ConnectionStrings:DefaultConnection для веб-проекта ShoeStore.");
        var settings = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
            SearchPath = "pg_temp"
        };
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync();

        // Таблицы и данные доступны только в этом подключении.
        await using var setup = new NpgsqlCommand("""
            CREATE TEMP TABLE "Brands" ("Id" integer PRIMARY KEY, "Name" text);
            CREATE TEMP TABLE "Products" (
                "Id" integer PRIMARY KEY, "BrandId" integer, "Name" text,
                "Price" numeric(10, 2), "ImageUrl" text,
                "IsNew" boolean, "IsPopular" boolean, "Status" text);
            INSERT INTO "Brands" VALUES (1, 'Учебный бренд');
            INSERT INTO "Products"
                SELECT id, 1, 'Товар ' || id, 10000 + id * 100 + 0.30,
                    '/images/products/9060.jpg', id > 16, id % 3 = 0, 'Active'
                FROM generate_series(1, 18) AS id;
            INSERT INTO "Products" VALUES
                (100, 1, 'Черновик', 1, '', false, true, 'Draft'),
                (101, 1, 'Архив', 1, '', false, true, 'Archived');
            """, connection);
        await setup.ExecuteNonQueryAsync();

        await using var snapshot = new NpgsqlCommand("""
            SELECT jsonb_build_object(
                'products', (SELECT jsonb_agg(to_jsonb(p) ORDER BY p."Id") FROM "Products" p),
                'brands', (SELECT jsonb_agg(to_jsonb(b) ORDER BY b."Id") FROM "Brands" b))::text
            """, connection);
        var before = (string)(await snapshot.ExecuteScalarAsync())!;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
        await using var db = new ApplicationDbContext(options);

        var result = await new CatalogQueryService(db).GetHomeAsync(CancellationToken.None);

        Assert.HasCount(18, result.Products);
        CollectionAssert.AreEquivalent(Enumerable.Range(1, 18).ToArray(), result.Products.Select(product => product.Id).ToArray());
        var product = result.Products.Single(item => item.Id == 17);
        Assert.AreEqual("Товар 17", product.Name);
        Assert.AreEqual("Учебный бренд", product.BrandName);
        Assert.AreEqual(11700.30m, product.Price);
        Assert.AreEqual("/images/products/9060.jpg", product.ImageUrl);
        Assert.AreEqual(before, (string)(await snapshot.ExecuteScalarAsync())!);
    }
}
