using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Integration;

[TestClass]
public class TSortingDatabase
{
    [TestMethod]
    [TestCategory("Sorting")]
    public async Task GetHomeAsync_PostgreSqlFiltersAndOrdersWithoutChangingData()
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

        // Временные таблицы существуют только в этом подключении.
        await using var setup = new NpgsqlCommand("""
            CREATE TEMP TABLE "Brands" ("Id" integer PRIMARY KEY, "Name" text, "Description" text);
            CREATE TEMP TABLE "Products" (
                "Id" integer PRIMARY KEY, "BrandId" integer, "Name" text,
                "Price" numeric(10, 2), "ImageUrl" text DEFAULT '',
                "IsNew" boolean, "IsPopular" boolean, "Status" text,
                "Description" text DEFAULT '', "OldPrice" numeric(10, 2),
                "CategoryId" integer DEFAULT 1, "CreatedAt" timestamptz DEFAULT now(),
                "UpdatedAt" timestamptz DEFAULT now());
            CREATE TEMP TABLE "ProductVariants" (
                "Id" integer PRIMARY KEY, "ProductId" integer, "Name" text,
                "Sku" text, "Swatch" text, "ImageUrl" text, "ThumbnailUrl" text,
                "IsDefault" boolean);
            CREATE TEMP TABLE "ProductVariantSizes" (
                "Id" integer PRIMARY KEY, "ProductVariantId" integer,
                "Size" integer, "StockQuantity" integer);
            CREATE TEMP TABLE "ProductVariantImages" (
                "Id" integer PRIMARY KEY, "ProductVariantId" integer,
                "ImageUrl" text, "ThumbnailUrl" text, "SortOrder" integer);
            INSERT INTO "Brands" ("Id", "Name") VALUES (1, 'Тестовый бренд');
            INSERT INTO "Products" ("Id", "BrandId", "Name", "Price", "IsNew", "IsPopular", "Status")
            VALUES
                (1, 1, 'Модель 1', 20, false, true, 'Active'),
                (4, 1, 'Модель 4', 50, true, false, 'Active'),
                (8, 1, 'Модель 8', 70, true, true, 'Active'),
                (5, 1, 'Модель 5', 5, true, true, 'Active'),
                (2, 1, 'Черновик', 1, true, true, 'Draft'),
                (7, 1, 'Архивный товар', 999, true, true, 'Archived');
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
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var catalog = new CatalogQueryService(db, cache);

        var newProducts = await catalog.GetHomeAsync(new CatalogSelection("new", "price-asc"), CancellationToken.None);
        CollectionAssert.AreEqual(new[] { 5, 4, 8 }, newProducts.Products.Select(product => product.Id).ToArray());

        var popularProducts = await catalog.GetHomeAsync(new CatalogSelection("popular", "price-desc"), CancellationToken.None);
        CollectionAssert.AreEqual(new[] { 8, 1, 5 }, popularProducts.Products.Select(product => product.Id).ToArray());

        Assert.AreEqual(before, (string)(await snapshot.ExecuteScalarAsync())!);

        await connection.CloseAsync();
        var offlineOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1").Options;
        await using var offlineDb = new ApplicationDbContext(offlineOptions);
        var nextRequest = new CatalogQueryService(offlineDb, cache);
        var search = await nextRequest.GetHomeAsync(
            CatalogSelection.FromQuery("popular", "price-desc", " МОДЕЛЬ ", [1]), CancellationToken.None);
        CollectionAssert.AreEqual(new[] { 8, 1, 5 }, search.Products.Select(product => product.Id).ToArray());
        CollectionAssert.AreEqual(new[] { 1 }, search.CatalogBrands.Keys.ToArray());
        Assert.AreSame(newProducts.Products.Single(product => product.Id == 5), nextRequest.GetProduct(5));
        Assert.IsNull(nextRequest.GetProduct(2));
        Assert.IsNull(nextRequest.GetProduct(7));
    }
}
