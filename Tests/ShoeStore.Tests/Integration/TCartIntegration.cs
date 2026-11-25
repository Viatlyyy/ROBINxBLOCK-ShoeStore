using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using ShoeStore.Data;

namespace ShoeStore.Tests.Integration;

[TestClass]
public class TCartIntegration
{
    private NpgsqlConnection connection = null!;
    private WebApplicationFactory<Program> app = null!;
    private HttpClient client = null!;
    private string token = "";

    [TestInitialize]
    public async Task Setup()
    {
        var config = new ConfigurationBuilder().AddUserSecrets<Program>(optional: true).AddEnvironmentVariables().Build();
        var settings = new NpgsqlConnectionStringBuilder(config["SHOESTORE_TEST_CONNECTION_STRING"] ?? config.GetConnectionString("DefaultConnection"))
        {
            Pooling = false, SearchPath = "pg_temp"
        };
        connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        await using var seed = new NpgsqlCommand("""
            CREATE TEMP TABLE "Brands" ("Id" int, "Name" text, "Description" text);
            CREATE TEMP TABLE "Products" ("Id" int, "BrandId" int, "CategoryId" int, "Name" text,
                "Description" text, "Price" numeric, "OldPrice" numeric, "ImageUrl" text, "Status" text,
                "IsPopular" boolean, "IsNew" boolean, "CreatedAt" timestamptz, "UpdatedAt" timestamptz);
            CREATE TEMP TABLE "ProductVariants" ("Id" int, "ProductId" int, "Name" text, "Sku" text,
                "Swatch" text, "ImageUrl" text, "ThumbnailUrl" text, "IsDefault" boolean);
            CREATE TEMP TABLE "ProductVariantSizes" ("Id" int, "ProductVariantId" int, "Size" int, "StockQuantity" int);
            CREATE TEMP TABLE "ProductVariantImages" ("Id" int, "ProductVariantId" int, "ImageUrl" text,
                "ThumbnailUrl" text, "SortOrder" int);
            INSERT INTO "Brands" VALUES (1, 'Бренд', '');
            INSERT INTO "Products" VALUES (1, 1, 1, 'Модель', '', 2000, NULL, '', 'Active', true, false, now(), now());
            INSERT INTO "ProductVariants" VALUES (7, 1, 'Основной', 'A', '', '', NULL, true);
            INSERT INTO "ProductVariantSizes" SELECT s, 7, s, CASE s WHEN 36 THEN 0 WHEN 37 THEN 3 ELSE 20 END
                FROM generate_series(36, 49) s;
            """, connection);
        await seed.ExecuteNonQueryAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
        app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.AddScoped(_ => new ApplicationDbContext(options));
                var roles = new Mock<RoleManager<IdentityRole>>(Mock.Of<IRoleStore<IdentityRole>>(),
                    Array.Empty<IRoleValidator<IdentityRole>>(), new UpperInvariantLookupNormalizer(),
                    new IdentityErrorDescriber(), NullLogger<RoleManager<IdentityRole>>.Instance);
                roles.Setup(manager => manager.RoleExistsAsync("Customer")).ReturnsAsync(true);
                services.RemoveAll<RoleManager<IdentityRole>>();
                services.AddSingleton(roles.Object);
            });
        });
        client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await client.GetStringAsync("/");
        var html = await client.GetStringAsync("/Catalog/Details/1");
        token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrEmpty(token));
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        client?.Dispose();
        if (app is not null) await app.DisposeAsync();
        if (connection is not null) await connection.DisposeAsync();
    }

    private Task<HttpResponseMessage> Post(string action, int size, int quantity = 1, int change = 1) =>
        client.PostAsync("/Cart/" + action, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["variantId"] = "7", ["size"] = size.ToString(),
            ["quantity"] = quantity.ToString(), ["change"] = change.ToString()
        }));

    [TestMethod]
    public async Task Cart_CombinesSameSizeSeparatesSizesAndRecalculatesTotal()
    {
        (await Post("Add", 37, 2)).EnsureSuccessStatusCode();
        (await Post("Add", 37, 2)).EnsureSuccessStatusCode();
        (await Post("Add", 38, 2)).EnsureSuccessStatusCode();
        var result = await (await Post("ChangeQuantity", 37, change: -1)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(2, result.GetProperty("quantity").GetInt32());
        Assert.AreEqual(4000m, result.GetProperty("lineTotal").GetDecimal());
        Assert.AreEqual(8000m, result.GetProperty("total").GetDecimal());
        Assert.AreEqual(4, result.GetProperty("count").GetInt32());

        var cart = await client.GetStringAsync("/Cart");
        Assert.AreEqual(2, Regex.Matches(cart, "data-cart-line>").Count);
        using var guest = app.CreateClient();
        StringAssert.Contains(WebUtility.HtmlDecode(await guest.GetStringAsync("/Cart")), "Корзина пока пуста");
        Assert.AreEqual(HttpStatusCode.Redirect, (await Post("Remove", 37)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Redirect, (await Post("Remove", 38)).StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")), "Корзина пока пуста");
    }

    [TestMethod]
    public async Task Cart_KeepsBothAdditionsFromConcurrentRequests()
    {
        var responses = await Task.WhenAll(Post("Add", 38), Post("Add", 38));
        foreach (var response in responses) response.EnsureSuccessStatusCode();
        var html = await client.GetStringAsync("/Cart");
        StringAssert.Contains(html, "data-cart-line-quantity>2</span>");
    }

    [TestMethod]
    public async Task Cart_RejectsMissingTokenAndArchivedProduct()
    {
        using var invalid = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["variantId"] = "7", ["size"] = "38", ["quantity"] = "1"
        });
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsync("/Cart/Add", invalid)).StatusCode);
        await using var archive = new NpgsqlCommand("""UPDATE "Products" SET "Status" = 'Archived' WHERE "Id" = 1""", connection);
        await archive.ExecuteNonQueryAsync();
        Assert.AreEqual(HttpStatusCode.BadRequest, (await Post("Add", 38)).StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")), "Корзина пока пуста");
    }

    [TestMethod]
    public async Task Cart_EnforcesStockQuantityAndLineLimits()
    {
        Assert.AreEqual(HttpStatusCode.BadRequest, (await Post("Add", 36)).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await Post("Add", 38, 11)).StatusCode);
        for (int size = 37; size < 47; size++)
            (await Post("Add", size, 10)).EnsureSuccessStatusCode();
        Assert.AreEqual(HttpStatusCode.BadRequest, (await Post("Add", 47)).StatusCode);
        var capped = await (await Post("ChangeQuantity", 38)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(10, capped.GetProperty("quantity").GetInt32());

        await using var update = new NpgsqlCommand("""UPDATE "ProductVariantSizes" SET "StockQuantity" = 0 WHERE "Size" = 38""", connection);
        await update.ExecuteNonQueryAsync();
        var remaining = await (await Post("ChangeQuantity", 38)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(0, remaining.GetProperty("quantity").GetInt32());
        Assert.AreEqual(9, remaining.GetProperty("lineCount").GetInt32());
        Assert.AreEqual(83, remaining.GetProperty("count").GetInt32());
    }
}
