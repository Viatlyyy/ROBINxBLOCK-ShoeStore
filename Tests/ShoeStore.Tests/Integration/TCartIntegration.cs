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
using ShoeStore.Models;
using ShoeStore.Services;

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
        Assert.AreEqual("shoestore2", settings.Database, "Настройте проверочное подключение к shoestore2.");
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
            CREATE TEMP TABLE "Orders" ("Id" int GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                "PublicId" uuid UNIQUE, "UserId" text, "CustomerName" text, "LastName" text, "Email" text,
                "Phone" text, "DeliveryMethod" text, "City" text, "Street" text, "House" text, "Apartment" text,
                "Postcode" text, "Comment" text, "PaymentMethod" text, "Status" text, "CreatedAt" timestamptz, "Total" numeric);
            CREATE TEMP TABLE "OrderItems" ("Id" int GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                "OrderId" int REFERENCES "Orders", "ProductId" int, "ProductVariantId" int, "ProductName" text,
                "VariantName" text, "Sku" text, "Size" int, "Quantity" int, "Price" numeric, "ImageUrl" text);
            INSERT INTO "Brands" VALUES (1, 'Бренд', '');
            INSERT INTO "Products" VALUES (1, 1, 1, 'Модель', '', 2000, NULL, '', 'Active', true, false, now(), now());
            INSERT INTO "ProductVariants" VALUES (7, 1, 'Основной', 'A', '', '', NULL, true);
            INSERT INTO "ProductVariants" VALUES
                (8, 1, 'Мятный', 'B', '#39a788', '/images/products/530.jpg', NULL, false);
            INSERT INTO "ProductVariantSizes" VALUES (100, 8, 37, 1), (101, 8, 50, 2);
            INSERT INTO "ProductVariantImages" VALUES (200, 8, '/images/products/9060.jpg', NULL, 1);
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

    private Task<HttpResponseMessage> Post(string action, int size, int quantity = 1, int change = 1, int variantId = 7) =>
        client.PostAsync("/Cart/" + action, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["variantId"] = variantId.ToString(), ["size"] = size.ToString(),
            ["quantity"] = quantity.ToString(), ["change"] = change.ToString()
        }));

    private const string CheckoutUserId = "cart-test-customer";

    private async Task SignInForCheckoutAsync()
    {
        foreach (var table in new[] { "AspNetUsers", "AspNetUserClaims", "AspNetRoles", "AspNetUserRoles", "AspNetRoleClaims", "Favorites" })
        {
            await using var copy = new NpgsqlCommand($"""CREATE TEMP TABLE "{table}" (LIKE public."{table}" INCLUDING ALL)""", connection);
            await copy.ExecuteNonQueryAsync();
        }
        const string email = "cart-customer@example.com";
        const string password = "CheckoutTest1!";
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.IsTrue((await users.CreateAsync(
                new ApplicationUser { Id = CheckoutUserId, UserName = email, Email = email }, password)).Succeeded);
        }

        var login = await client.GetStringAsync("/Account/Login");
        using var fields = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = password,
            ["__RequestVerificationToken"] = Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value
        });
        Assert.AreEqual(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Login", fields)).StatusCode);
        var product = await client.GetStringAsync("/Catalog/Details/1");
        token = Regex.Match(product, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrEmpty(token));
    }

    [TestMethod]
    public async Task Checkout_OffersPaymentOnReceiptWithoutCollectingCardDetails()
    {
        await SignInForCheckoutAsync();
        (await Post("Add", 37)).EnsureSuccessStatusCode();
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Cart/Checkout"));
        StringAssert.Contains(html, "Оплата при получении");
        Assert.IsFalse(html.Contains("Номер карты", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("Apple Pay", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("Google Pay", StringComparison.Ordinal));

        var confirmation = Regex.Match(html, "name=\"confirmation\" value=\"([^\"]+)\"").Groups[1].Value;
        using var data = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["confirmation"] = confirmation,
            ["CustomerName"] = "Покупатель", ["Phone"] = "+79000000000",
            ["DeliveryMethod"] = "Самовывоз", ["PaymentMethod"] = "card"
        });
        var response = await client.PostAsync("/Cart/Checkout", data);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), "Выберите доступный способ оплаты");
    }

    [TestMethod]
    public async Task Checkout_RequiresItemsAndConfirmationWithCurrentQuantity()
    {
        await SignInForCheckoutAsync();
        Assert.AreEqual(HttpStatusCode.Redirect, (await client.GetAsync("/Cart/Checkout")).StatusCode);
        (await Post("Add", 37)).EnsureSuccessStatusCode();
        var html = await client.GetStringAsync("/Cart/Checkout");
        var confirmation = Regex.Match(html, "name=\"confirmation\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.IsTrue(Guid.TryParse(confirmation, out _));
        (await Post("Add", 37)).EnsureSuccessStatusCode();
        using var data = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["confirmation"] = confirmation,
            ["CustomerName"] = "Покупатель", ["Phone"] = "+79000000000",
            ["DeliveryMethod"] = "Самовывоз", ["PaymentMethod"] = "cash"
        });
        var response = await client.PostAsync("/Cart/Checkout", data);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), "Корзина изменилась");
        StringAssert.Contains(await client.GetStringAsync("/Cart"), "data-cart-line-quantity>2</span>");
    }

    [TestMethod]
    public async Task Checkout_SavesOrderOnceClearsCartAndUpdatesStock()
    {
        await SignInForCheckoutAsync();
        (await Post("Add", 37, 3)).EnsureSuccessStatusCode();
        var html = await client.GetStringAsync("/Cart/Checkout");
        var confirmation = Regex.Match(html, "name=\"confirmation\" value=\"([^\"]+)\"").Groups[1].Value;
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["confirmation"] = confirmation,
            ["CustomerName"] = "Покупатель", ["Phone"] = "+79000000000", ["DeliveryMethod"] = "Курьер",
            ["PaymentMethod"] = "cash"
        };
        using var invalid = new FormUrlEncodedContent(fields);
        var validation = await client.PostAsync("/Cart/Checkout", invalid);
        StringAssert.Contains(WebUtility.HtmlDecode(await validation.Content.ReadAsStringAsync()), "Введите улицу.");
        fields["City"] = "Москва";
        fields["Street"] = "Тестовая";
        fields["House"] = "1";
        using var first = new FormUrlEncodedContent(fields);
        using var repeat = new FormUrlEncodedContent(fields);
        var responses = await Task.WhenAll(client.PostAsync("/Cart/Checkout", first), client.PostAsync("/Cart/Checkout", repeat));
        foreach (var response in responses) Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        var success = responses.Single(response => response.Headers.Location!.OriginalString.Contains("/Success/"));
        var receipt = WebUtility.HtmlDecode(await client.GetStringAsync(success.Headers.Location));
        StringAssert.Contains(receipt, "Заказ №1 создан");
        Assert.IsFalse(receipt.Contains(fields["Phone"]));
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/Cart/Success/{Guid.NewGuid()}")).StatusCode);
        var details = await client.GetStringAsync("/Catalog/Details/1");
        var defaultSizes = Regex.Match(details, """data-size-options="7"[^>]*>(.*?)</div>""", RegexOptions.Singleline);
        Assert.IsTrue(defaultSizes.Success);
        Assert.IsFalse(defaultSizes.Value.Contains("value=\"37\" data-stock", StringComparison.Ordinal));
        StringAssert.Contains(WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")), "Корзина пока пуста");
        await using var check = new NpgsqlCommand("""
            SELECT count(*) = 1 AND min(o."Total") = 6000 AND min(i."Price") = 2000 AND min(i."Quantity") = 3
                AND min(i."Size") = 37 AND min(o."Status") = 'Новый' AND min(o."Street") = 'Тестовая'
                AND (SELECT "StockQuantity" FROM "ProductVariantSizes" WHERE "ProductVariantId" = 7 AND "Size" = 37) = 0
            FROM "Orders" o JOIN "OrderItems" i ON i."OrderId" = o."Id"
            """, connection);
        Assert.AreEqual(true, await check.ExecuteScalarAsync());
    }

    [TestMethod]
    public async Task Checkout_RollsBackAllSizesWhenLastPairIsUnavailable()
    {
        await SignInForCheckoutAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var variant = await db.ProductVariants.AsNoTracking().Include(v => v.Product).ThenInclude(p => p.Brand).SingleAsync(v => v.Id == 7);
        var service = scope.ServiceProvider.GetRequiredService<CheckoutService>();
        var result = await service.PlaceOrderAsync(new CheckoutViewModel(), [
            new CartLine { Variant = variant, Size = 37, Quantity = 1 },
            new CartLine { Variant = variant, Size = 50, Quantity = 1 }
        ], CheckoutUserId, Guid.NewGuid(), CancellationToken.None);
        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Error, "размер 50");
        Assert.AreEqual(3, await db.ProductVariantSizes.Where(s => s.ProductVariantId == 7 && s.Size == 37).Select(s => s.StockQuantity).SingleAsync());
        Assert.AreEqual(0, await db.Orders.CountAsync());
    }

    [TestMethod]
    public async Task ProductColors_RenderSwatchesAndOpenSelectedVariant()
    {
        foreach (var page in new[] { "/", "/Catalog", "/Catalog/SearchResults?search=Модель" })
        {
            var html = WebUtility.HtmlDecode(await client.GetStringAsync(page));
            StringAssert.Contains(html, "data-variant-id=\"8\"");
            StringAssert.Contains(html, "Мятный");
        }

        var selected = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog/Details/1?variantId=8"));
        StringAssert.Contains(selected, "name=\"variantId\" value=\"8\"");
        StringAssert.Contains(selected, "data-gallery-images=\"8\"");
        StringAssert.Contains(selected, "value=\"50\" data-stock=\"2\"");

        var fallback = await client.GetStringAsync("/Catalog/Details/1?variantId=12345");
        StringAssert.Contains(fallback, "name=\"variantId\" value=\"7\"");
    }

    [TestMethod]
    public async Task ProductColors_DetailsLoadsProductAbsentFromCatalogCache()
    {
        await using var newProduct = new NpgsqlCommand("""
            INSERT INTO "Products" VALUES (2, 1, 1, 'Прямая ссылка', '', 2000, NULL, '', 'Active', false, false, now(), now());
            INSERT INTO "ProductVariants" VALUES (9, 2, 'Синий', 'C', '#0033aa', '/images/products/530.jpg', NULL, true);
            INSERT INTO "ProductVariantSizes" VALUES (102, 9, 37, 2);
            """, connection);
        await newProduct.ExecuteNonQueryAsync();

        var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog/Details/2?variantId=9"));
        StringAssert.Contains(page, "Прямая ссылка");
        StringAssert.Contains(page, "name=\"variantId\" value=\"9\"");
    }

    [TestMethod]
    public async Task ProductColors_CheckoutKeepsVariantsSeparateAndRefreshesSelectedStock()
    {
        await SignInForCheckoutAsync();
        (await Post("Add", 37)).EnsureSuccessStatusCode();
        Assert.AreEqual(HttpStatusCode.BadRequest, (await Post("Add", 38, variantId: 8)).StatusCode);
        (await Post("Add", 37, variantId: 8)).EnsureSuccessStatusCode();

        var cart = WebUtility.HtmlDecode(await client.GetStringAsync("/Cart"));
        Assert.AreEqual(2, Regex.Matches(cart, "data-cart-line>").Count);
        StringAssert.Contains(cart, "Цвет: Основной");
        StringAssert.Contains(cart, "Цвет: Мятный");
        StringAssert.Contains(cart, "/Catalog/Details/1?variantId=8");

        var checkout = await client.GetStringAsync("/Cart/Checkout");
        var confirmation = Regex.Match(checkout, "name=\"confirmation\" value=\"([^\"]+)\"").Groups[1].Value;
        using var data = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["confirmation"] = confirmation,
            ["CustomerName"] = "Покупатель", ["Phone"] = "+79000000000",
            ["DeliveryMethod"] = "Самовывоз", ["PaymentMethod"] = "cash"
        });
        var response = await client.PostAsync("/Cart/Checkout", data);
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        StringAssert.Contains(response.Headers.Location?.OriginalString, "/Success/");

        var details = await client.GetStringAsync("/Catalog/Details/1?variantId=8");
        StringAssert.Contains(details, "name=\"variantId\" value=\"8\"");
        StringAssert.Contains(details, "value=\"37\" data-stock=\"2\"");
        Assert.IsFalse(details.Contains("value=\"37\" data-stock=\"1\"", StringComparison.Ordinal));

        await using var check = new NpgsqlCommand("""
            SELECT count(*) = 2 AND bool_or("ProductVariantId" = 7 AND "Size" = 37)
                AND bool_or("ProductVariantId" = 8 AND "Size" = 37 AND "VariantName" = 'Мятный')
            FROM "OrderItems"
            """, connection);
        Assert.AreEqual(true, await check.ExecuteScalarAsync());
    }

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
