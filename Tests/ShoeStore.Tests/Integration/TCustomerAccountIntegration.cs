using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Tests.Integration;

[TestClass]
public class TCustomerAccountIntegration
{
    private const string Password = "CabinetTest1!";
    private const string OwnOrder = "12345678-1234-1234-1234-123456789012";
    private const string OtherOrder = "33333333-3333-3333-3333-333333333333";
    private NpgsqlConnection connection = null!;
    private WebApplicationFactory<Program> app = null!;

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
        foreach (var table in new[] { "AspNetUsers", "AspNetUserClaims", "AspNetRoles", "AspNetUserRoles", "AspNetRoleClaims",
            "Brands", "Products", "ProductVariants", "ProductVariantSizes", "ProductVariantImages", "Favorites", "Orders", "OrderItems" })
        {
            await using var copy = new NpgsqlCommand($"""CREATE TEMP TABLE "{table}" (LIKE public."{table}" INCLUDING ALL)""", connection);
            await copy.ExecuteNonQueryAsync();
        }
        await using var seed = new NpgsqlCommand("""
            INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName") VALUES ('test-customer', 'Customer', 'CUSTOMER');
            INSERT INTO "Brands" ("Id", "Name", "Description") VALUES (1, 'Тестовый бренд', '');
            INSERT INTO "Products" ("Id", "BrandId", "CategoryId", "Name", "Description", "Price", "ImageUrl",
                 "Status", "IsPopular", "IsNew", "CreatedAt", "UpdatedAt")
              VALUES (1, 1, 1, 'Тестовая модель', '', 22990, '/images/products/9060.jpg', 'Active', true, false, now(), now());
            INSERT INTO "ProductVariants" ("Id", "ProductId", "Name", "Sku", "Swatch", "ImageUrl", "IsDefault")
              VALUES (7, 1, 'Black / Castlerock', 'TEST-9060', '#333333', '/images/products/9060.jpg', true);
            INSERT INTO "ProductVariantSizes" ("Id", "ProductVariantId", "Size", "StockQuantity") VALUES (1, 7, 42, 3);
            INSERT INTO "Orders" ("Id", "PublicId", "UserId", "CustomerName", "Phone", "DeliveryMethod", "Status", "CreatedAt", "Total")
              VALUES (1042, '12345678-1234-1234-1234-123456789012', 'account-a', 'Первый покупатель', '+79000000000', 'Самовывоз', 'Новый', now(), 22990),
                     (1018, '33333333-3333-3333-3333-333333333333', 'account-b', 'Другой покупатель', '+79000000001', 'Самовывоз', 'Выполнен', now(), 14490),
                     (1019, '44444444-4444-4444-4444-444444444444', NULL, 'Гость', '+79000000000', 'Самовывоз', 'Новый', now(), 14490);
            INSERT INTO "OrderItems" ("OrderId", "ProductId", "ProductVariantId", "ProductName", "VariantName", "Sku", "Size", "Quantity", "Price", "ImageUrl")
              VALUES (1042, 1, 7, 'Снимок модели при покупке', 'Black / Castlerock', 'TEST-9060', 42, 1, 22990, '/images/products/9060.jpg');
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
            });
        });
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (id, email, name) in new[]
        {
            ("account-a", "alex@example.com", "Алексей"),
            ("account-b", "other@example.com", "Другой покупатель")
        })
        {
            var user = new ApplicationUser { Id = id, UserName = email, Email = email };
            Assert.IsTrue((await users.CreateAsync(user, Password)).Succeeded);
            Assert.IsTrue((await users.AddClaimAsync(user, new Claim(ClaimTypes.GivenName, name))).Succeeded);
        }
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (app is not null) await app.DisposeAsync();
        if (connection is not null) await connection.DisposeAsync();
    }

    private HttpClient Client() => app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string Token(string html)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]+value=\"([^\"]+)\"").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrEmpty(token));
        return WebUtility.HtmlDecode(token);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, string html,
        Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = Token(html);
        using var data = new FormUrlEncodedContent(fields);
        return await client.PostAsync(path, data);
    }

    private static async Task<HttpResponseMessage> Login(HttpClient client, string email = "alex@example.com", string? returnUrl = null)
    {
        var path = "/Account/Login" + (returnUrl is null ? "" : "?returnUrl=" + Uri.EscapeDataString(returnUrl));
        return await Post(client, path, await client.GetStringAsync(path), new()
        {
            ["Email"] = email, ["Password"] = Password, ["ReturnUrl"] = returnUrl ?? ""
        });
    }

    private const string CheckoutPath = "/Cart/Checkout";

    private static void AssertCheckoutLogin(HttpResponseMessage response)
    {
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        var redirect = new Uri(new Uri("http://localhost"), response.Headers.Location!);
        Assert.AreEqual("/Account/Login", redirect.AbsolutePath);
        Assert.AreEqual(CheckoutPath, Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(redirect.Query)["ReturnUrl"].ToString());
    }

    private static Task<HttpResponseMessage> AddToCart(HttpClient client, string product) =>
        Post(client, "/Cart/Add", product, new() { ["variantId"] = "7", ["size"] = "42", ["quantity"] = "2" });

    private static string Confirmation(string html)
    {
        var value = Regex.Match(html, "name=\"confirmation\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.IsTrue(Guid.TryParse(value, out _));
        return value;
    }

    private async Task AssertNoNewOrdersAsync()
    {
        await using var check = new NpgsqlCommand("""
            SELECT NOT EXISTS (SELECT 1 FROM "Orders" WHERE "Id" < 1000)
                AND (SELECT "StockQuantity" FROM "ProductVariantSizes" WHERE "ProductVariantId" = 7 AND "Size" = 42) = 3
            """, connection);
        Assert.AreEqual(true, await check.ExecuteScalarAsync());
    }

    [TestMethod]
    public async Task GuestCheckout_LoginReturnsToTheSameCart()
    {
        using var client = Client();
        var product = await client.GetStringAsync("/Catalog/Details/1");
        Assert.AreEqual(HttpStatusCode.Redirect, (await AddToCart(client, product)).StatusCode);
        var cart = WebUtility.HtmlDecode(await client.GetStringAsync("/Cart"));
        StringAssert.Contains(cart, "Black / Castlerock");
        StringAssert.Contains(cart, "Войдите, чтобы оформить заказ");
        AssertCheckoutLogin(await client.GetAsync(CheckoutPath));

        var signIn = await Login(client, returnUrl: CheckoutPath);
        Assert.AreEqual(CheckoutPath, signIn.Headers.Location?.OriginalString);
        var checkout = WebUtility.HtmlDecode(await client.GetStringAsync(CheckoutPath));
        StringAssert.Contains(checkout, "Black / Castlerock");
        StringAssert.Contains(checkout, "Размер: 42");
        StringAssert.Contains(checkout, "2 ×");
        _ = Confirmation(checkout);
        await AssertNoNewOrdersAsync();
    }

    [TestMethod]
    public async Task GuestCheckout_PostWithAntiforgeryTokenRequiresLogin()
    {
        using var client = Client();
        var product = await client.GetStringAsync("/Catalog/Details/1");
        Assert.AreEqual(HttpStatusCode.Redirect, (await AddToCart(client, product)).StatusCode);
        var response = await Post(client, CheckoutPath, product, new()
        {
            ["confirmation"] = Guid.NewGuid().ToString(), ["CustomerName"] = "Гость", ["Phone"] = "+79000000000",
            ["DeliveryMethod"] = "Самовывоз", ["PaymentMethod"] = "cash"
        });
        AssertCheckoutLogin(response);
        await AssertNoNewOrdersAsync();
    }

    [TestMethod]
    public async Task GuestCheckout_RegistrationAndLoginReturnToCheckout()
    {
        using var client = Client();
        Assert.AreEqual(HttpStatusCode.Redirect, (await AddToCart(client, await client.GetStringAsync("/Catalog/Details/1"))).StatusCode);
        var gate = await client.GetAsync(CheckoutPath);
        AssertCheckoutLogin(gate);
        var login = await client.GetStringAsync(gate.Headers.Location);
        var registerPath = Regex.Match(login, "href=\"(/Account/Register\\?[^\"]+)\"").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrEmpty(registerPath));
        var register = await client.GetStringAsync(WebUtility.HtmlDecode(registerPath));
        var created = await Post(client, "/Account/Register", register, new()
        {
            ["ReturnUrl"] = CheckoutPath, ["FirstName"] = "Новый покупатель", ["Email"] = "new@example.com",
            ["Password"] = Password, ["ConfirmPassword"] = Password, ["AcceptTerms"] = "true"
        });
        AssertCheckoutLogin(created);
        var signIn = await Login(client, "new@example.com", CheckoutPath);
        Assert.AreEqual(CheckoutPath, signIn.Headers.Location?.OriginalString);
        StringAssert.Contains(WebUtility.HtmlDecode(await client.GetStringAsync(CheckoutPath)), "Black / Castlerock");
    }

    [TestMethod]
    public async Task GuestCheckout_OldConfirmationCannotBeSubmittedAfterSwitchingAccounts()
    {
        using var client = Client();
        Assert.AreEqual(HttpStatusCode.Redirect, (await AddToCart(client, await client.GetStringAsync("/Catalog/Details/1"))).StatusCode);
        await Login(client);
        var first = await client.GetStringAsync(CheckoutPath);
        var original = Confirmation(first);
        Assert.AreEqual(HttpStatusCode.Redirect, (await Post(client, "/Account/Logout", first, new())).StatusCode);

        await Login(client, "other@example.com", CheckoutPath);
        var second = await client.GetStringAsync(CheckoutPath);
        Assert.AreNotEqual(original, Confirmation(second));
        var response = await Post(client, CheckoutPath, second, new()
        {
            ["confirmation"] = original, ["CustomerName"] = "Другой покупатель", ["Phone"] = "+79000000001",
            ["DeliveryMethod"] = "Самовывоз", ["PaymentMethod"] = "cash"
        });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), "Корзина изменилась");
        await AssertNoNewOrdersAsync();
    }

    [TestMethod]
    public async Task GuestCheckout_AccountDisappearingDuringSaveRequiresLoginAndRollsBack()
    {
        using var client = Client();
        await Login(client);
        Assert.AreEqual(HttpStatusCode.Redirect, (await AddToCart(client, await client.GetStringAsync("/Catalog/Details/1"))).StatusCode);
        var checkout = await client.GetStringAsync(CheckoutPath);

        await using var simulateDeletion = new NpgsqlCommand("""
            ALTER TABLE "Orders" ADD CONSTRAINT "FK_Orders_AspNetUsers_UserId"
                FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE SET NULL;
            CREATE FUNCTION pg_temp.delete_checkout_user() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                DELETE FROM pg_temp."AspNetUsers" WHERE "Id" = NEW."UserId";
                RETURN NEW;
            END;
            $$;
            CREATE TRIGGER test_delete_checkout_user BEFORE INSERT ON "Orders"
                FOR EACH ROW EXECUTE FUNCTION pg_temp.delete_checkout_user();
            """, connection);
        await simulateDeletion.ExecuteNonQueryAsync();

        var response = await Post(client, CheckoutPath, checkout, new()
        {
            ["confirmation"] = Confirmation(checkout), ["CustomerName"] = "Покупатель", ["Phone"] = "+79000000001",
            ["DeliveryMethod"] = "Самовывоз", ["PaymentMethod"] = "cash"
        });
        AssertCheckoutLogin(response);
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync(response.Headers.Location)).StatusCode);
        StringAssert.Contains(await client.GetStringAsync("/Cart"), "data-cart-line-quantity>2</span>");
        await AssertNoNewOrdersAsync();
    }

    [TestMethod]
    public async Task GuestCheckout_DeletedAccountReturnsToLoginAndKeepsCart()
    {
        using var client = Client();
        Assert.AreEqual(HttpStatusCode.Redirect, (await AddToCart(client, await client.GetStringAsync("/Catalog/Details/1"))).StatusCode);
        await Login(client);
        await using var delete = new NpgsqlCommand("""DELETE FROM "AspNetUsers" WHERE "Id" = 'account-a'""", connection);
        await delete.ExecuteNonQueryAsync();

        var response = await client.GetAsync(CheckoutPath);
        AssertCheckoutLogin(response);
        var login = await client.GetAsync(response.Headers.Location);
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        var signIn = await Login(client, "other@example.com", CheckoutPath);
        Assert.AreEqual(CheckoutPath, signIn.Headers.Location?.OriginalString);
        StringAssert.Contains(WebUtility.HtmlDecode(await client.GetStringAsync(CheckoutPath)), "Black / Castlerock");
        await AssertNoNewOrdersAsync();
    }


    [TestMethod]
    public async Task Account_RequiresLoginAndOnlyShowsOwnedOrders()
    {
        using var client = Client();
        var challenge = await client.GetAsync("/Account/Orders");
        Assert.AreEqual(HttpStatusCode.Redirect, challenge.StatusCode);
        StringAssert.Contains(challenge.Headers.Location?.OriginalString, "/Account/Login");
        var login = await Login(client, returnUrl: "/Account/Orders");
        Assert.AreEqual("/Account/Orders", login.Headers.Location?.OriginalString);

        var overview = WebUtility.HtmlDecode(await client.GetStringAsync("/Account"));
        StringAssert.Contains(overview, "Последний заказ");
        StringAssert.Contains(overview, "1042");
        var orders = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Orders"));
        StringAssert.Contains(orders, "1042");
        Assert.IsFalse(orders.Contains("1018", StringComparison.Ordinal));
        Assert.IsFalse(orders.Contains("1019", StringComparison.Ordinal));
        var details = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Orders/" + OwnOrder));
        StringAssert.Contains(details, "Снимок модели при покупке");
        StringAssert.Contains(details, "Black / Castlerock");
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/Account/Orders/" + OtherOrder)).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/Account/Orders/" + Guid.NewGuid())).StatusCode);
    }

    [TestMethod]
    public async Task Account_FavoritesRequireTokenAndPersistForOnlyCurrentUser()
    {
        using var client = Client();
        var productPage = "/Catalog/Details/1?variantId=7";
        var guest = WebUtility.HtmlDecode(await client.GetStringAsync(productPage));
        StringAssert.Contains(guest, "returnUrl=");
        var login = await Login(client, returnUrl: productPage);
        Assert.AreEqual(productPage, login.Headers.Location?.OriginalString);
        var page = await client.GetStringAsync(productPage);
        using var withoutToken = new FormUrlEncodedContent(new Dictionary<string, string> { ["productId"] = "1", ["isFavorite"] = "true" });
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Favorites", withoutToken)).StatusCode);
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.AreEqual(HttpStatusCode.Redirect, (await Post(client, "/Account/Favorites", page,
                new() { ["productId"] = "1", ["isFavorite"] = "true", ["returnUrl"] = productPage })).StatusCode);

        using var reopened = Client();
        await Login(reopened);
        var favorites = WebUtility.HtmlDecode(await reopened.GetStringAsync("/Account/Favorites"));
        Assert.AreEqual(1, Regex.Matches(favorites, "data-product-card").Count);
        StringAssert.Contains(favorites, "Тестовая модель");
        using var other = Client();
        var safeRedirect = await Login(other, "other@example.com", "https://example.com/");
        Assert.AreNotEqual("https://example.com/", safeRedirect.Headers.Location?.OriginalString);
        var otherFavorites = WebUtility.HtmlDecode(await other.GetStringAsync("/Account/Favorites"));
        StringAssert.Contains(otherFavorites, "Пока нет избранных товаров");
        Assert.AreEqual(HttpStatusCode.Redirect, (await Post(other, "/Account/Favorites", otherFavorites,
            new() { ["productId"] = "1", ["isFavorite"] = "false" })).StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(await reopened.GetStringAsync("/Account/Favorites")), "Тестовая модель");

        Assert.AreEqual(HttpStatusCode.Redirect, (await Post(reopened, "/Account/Favorites", favorites,
            new() { ["productId"] = "1", ["isFavorite"] = "false", ["returnUrl"] = "/Account/Favorites" })).StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(await reopened.GetStringAsync("/Account/Favorites")), "Пока нет избранных товаров");
    }

    [TestMethod]
    public async Task Account_FavoritesLoadsProductPublishedAfterCatalogWasCached()
    {
        using var client = Client();
        await Login(client);
        await client.GetStringAsync("/Catalog");
        await using var publish = new NpgsqlCommand("""
            INSERT INTO "Products" ("Id", "BrandId", "CategoryId", "Name", "Description", "Price",
                "Status", "IsPopular", "IsNew", "CreatedAt", "UpdatedAt")
              VALUES (2, 1, 1, 'Недавно опубликованная модель', '', 12990, 'Active', false, true, now(), now())
            """, connection);
        await publish.ExecuteNonQueryAsync();

        var product = await client.GetStringAsync("/Catalog/Details/2");
        Assert.AreEqual(HttpStatusCode.Redirect, (await Post(client, "/Account/Favorites", product,
            new() { ["productId"] = "2", ["isFavorite"] = "true" })).StatusCode);
        var favorites = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Favorites"));
        Assert.IsTrue(favorites.Contains("Недавно опубликованная модель", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Account_DisallowsFavoriteReadsAfterAccountWasDeleted()
    {
        using var client = Client();
        await Login(client);
        var product = await client.GetStringAsync("/Catalog/Details/1");
        Assert.AreEqual(HttpStatusCode.Redirect, (await Post(client, "/Account/Favorites", product,
            new() { ["productId"] = "1", ["isFavorite"] = "true" })).StatusCode);
        await using var delete = new NpgsqlCommand("""DELETE FROM "AspNetUsers" WHERE "Id" = 'account-a'""", connection);
        await delete.ExecuteNonQueryAsync();

        var details = await client.GetStringAsync("/Catalog/Details/1");
        Assert.IsFalse(Regex.IsMatch(details, "class=\"favorite-button\"[^>]+aria-pressed=\"true\""));
        Assert.AreEqual(HttpStatusCode.Redirect, (await client.GetAsync("/Account/Favorites")).StatusCode);
    }

    [TestMethod]
    public async Task Account_ProfileFailureKeepsBothPreviousValuesAndFormInput()
    {
        using var client = Client();
        await Login(client);
        var profile = await client.GetStringAsync("/Account/Profile");
        await using var rejectPhone = new NpgsqlCommand("""
            ALTER TABLE "AspNetUsers" ADD CONSTRAINT "test_reject_phone" CHECK ("PhoneNumber" IS NULL)
            """, connection);
        await rejectPhone.ExecuteNonQueryAsync();

        var rejected = await Post(client, "/Account/Profile", profile,
            new() { ["DisplayName"] = "Новое имя", ["Phone"] = "+79000009999" });
        Assert.AreEqual(HttpStatusCode.OK, rejected.StatusCode);
        var shown = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        StringAssert.Contains(shown, "Не удалось сохранить контакты");
        StringAssert.Contains(shown, "Новое имя");
        StringAssert.Contains(shown, "+79000009999");
        var unchanged = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Profile"));
        StringAssert.Contains(unchanged, "Алексей");
        Assert.IsFalse(unchanged.Contains("Новое имя", StringComparison.Ordinal));
        Assert.IsFalse(unchanged.Contains("+79000009999", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Account_ProfilePersistsValidContactsAndFillsCheckout()
    {
        using var client = Client();
        await Login(client);
        var profile = await client.GetStringAsync("/Account/Profile");
        var saved = await Post(client, "/Account/Profile", profile, new()
        {
            ["DisplayName"] = "Алексей Смирнов", ["Phone"] = "+79000001234", ["Email"] = "ignore@example.com"
        });
        Assert.AreEqual(HttpStatusCode.Redirect, saved.StatusCode);
        var updated = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Profile"));
        StringAssert.Contains(updated, "Алексей Смирнов");
        StringAssert.Contains(updated, "alex@example.com");
        StringAssert.Contains(updated, "+79000001234");
        Assert.IsFalse(updated.Contains("ignore@example.com", StringComparison.Ordinal));
        var invalid = await Post(client, "/Account/Profile", updated,
            new() { ["DisplayName"] = " ", ["Phone"] = "не телефон" });
        Assert.AreEqual(HttpStatusCode.OK, invalid.StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()), "не телефон");

        var product = await client.GetStringAsync("/Catalog/Details/1");
        var add = await Post(client, "/Cart/Add", product, new() { ["variantId"] = "7", ["size"] = "42", ["quantity"] = "1" });
        Assert.AreEqual(HttpStatusCode.Redirect, add.StatusCode);
        var checkout = WebUtility.HtmlDecode(await client.GetStringAsync("/Cart/Checkout"));
        StringAssert.Contains(checkout, "Алексей Смирнов");
        StringAssert.Contains(checkout, "alex@example.com");
        StringAssert.Contains(checkout, "+79000001234");
        var confirmation = Regex.Match(checkout, "name=\"confirmation\" value=\"([^\"]+)\"").Groups[1].Value;
        var created = await Post(client, "/Cart/Checkout", checkout, new()
        {
            ["confirmation"] = confirmation, ["CustomerName"] = "Алексей Смирнов", ["Phone"] = "+79000001234",
            ["Email"] = "alex@example.com", ["DeliveryMethod"] = "Самовывоз", ["PaymentMethod"] = "cash"
        });
        Assert.AreEqual(HttpStatusCode.Redirect, created.StatusCode);
        StringAssert.Contains(created.Headers.Location?.OriginalString, "/Cart/Success/");
        await using var check = new NpgsqlCommand("""
            SELECT "PublicId" FROM "Orders" WHERE "Id" = 1 AND "UserId" = 'account-a'
            """, connection);
        var id = await check.ExecuteScalarAsync();
        Assert.IsInstanceOfType<Guid>(id);
        var orders = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Orders"));
        StringAssert.Contains(orders, $"/Account/Orders/{id}");
        var details = WebUtility.HtmlDecode(await client.GetStringAsync($"/Account/Orders/{id}"));
        StringAssert.Contains(details, "Тестовая модель");
        StringAssert.Contains(details, "Размер 42");
    }
}
