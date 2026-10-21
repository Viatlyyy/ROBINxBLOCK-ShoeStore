using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Services;
using Match = System.Text.RegularExpressions.Match;

namespace ShoeStore.Tests.Integration;
[TestClass]
[DoNotParallelize]
public class THomePageIntegration
{
    private WebApplicationFactory<Program> application = null!;
    private HttpClient client = null!;
    private HomePageViewModel homeModel = null!;
    private Mock<ICatalogQueryService> catalog = null!;
    private Mock<DbConnectionInterceptor> databaseGuard = null!;

    [TestInitialize]
    public void Setup()
    {
        homeModel = new HomePageViewModel
        {
            Products = [new ProductCardViewModel
            {
                Id = 7001, Name = "Модель из базы", BrandName = "Тестовый бренд", Price = 12345m,
                ImageUrl = "/images/hero/9060-urban-feet-v6-960.webp"
            }],
            Brands = HomePageDemoData.Create().Brands
        };
        catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        catalog.Setup(service => service.GetHomeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(homeModel);
        catalog.Setup(service => service.GetHomeAsync(It.IsAny<CatalogSelection>(), It.IsAny<CancellationToken>()))
            .Returns((CatalogSelection selection, CancellationToken token) => Task.FromResult(new HomePageViewModel
            {
                Products = homeModel.Products, Brands = homeModel.Brands, Selection = selection
            }));

        var roles = new Mock<RoleManager<IdentityRole>>(
            Mock.Of<IRoleStore<IdentityRole>>(), Array.Empty<IRoleValidator<IdentityRole>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);
        roles.Setup(manager => manager.RoleExistsAsync("Customer")).ReturnsAsync(true);

        databaseGuard = new Mock<DbConnectionInterceptor>();
        databaseGuard.Setup(guard => guard.ConnectionOpening(It.IsAny<DbConnection>(),
                It.IsAny<ConnectionEventData>(), It.IsAny<InterceptionResult>()))
            .Throws(new InvalidOperationException("Тест страницы не должен открывать подключение к БД."));
        databaseGuard.Setup(guard => guard.ConnectionOpeningAsync(It.IsAny<DbConnection>(),
                It.IsAny<ConnectionEventData>(), It.IsAny<InterceptionResult>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Тест страницы не должен открывать подключение к БД."));

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "ShoeStore.csproj")))
            directory = directory.Parent ?? throw new DirectoryNotFoundException("Не найден веб-проект ShoeStore.");

        application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(directory.FullName);
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Host=127.0.0.1;Port=1;Database=unavailable_homepage_tests;Username=homepage_tests;Timeout=1;Command Timeout=1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICatalogQueryService>();
                services.AddSingleton(catalog.Object);
                services.RemoveAll<RoleManager<IdentityRole>>();
                services.AddSingleton(roles.Object);
                services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(databaseGuard.Object));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });

        client = application.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [TestCleanup]
    public void Cleanup()
    {
        client?.Dispose();
        application?.Dispose();
    }

    [TestMethod]
    public async Task Home_OpensWithoutAuthentication()
    {
        using var response = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    [TestCategory("CatalogTiles")]
    public async Task Home_RendersCatalogDataWithoutReplacingHero()
    {
        using var response = await client.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNull(response.Headers.Location);
        Assert.AreEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        var text = WebUtility.HtmlDecode(html);

        StringAssert.Contains(html, "id=\"catalog\"");
        StringAssert.Contains(text, "Каталог");
        Assert.IsFalse(text.Contains("Рекомендации", StringComparison.Ordinal));
        StringAssert.Contains(text, "New Balance");
        StringAssert.Contains(text, "9060");
        StringAssert.Contains(text, "3XL");
        StringAssert.Contains(text, "Dunk Low");
        Assert.AreEqual(3, Regex.Matches(html, "<article class=\"hero-slide").Count);
        var cards = Regex.Matches(html, "<article[^>]*data-product-card[^>]*>([\\s\\S]*?)</article>");
        Assert.AreEqual(1, cards.Count);
        StringAssert.Contains(WebUtility.HtmlDecode(cards[0].Value), "Модель из базы");
        StringAssert.Contains(WebUtility.HtmlDecode(cards[0].Value), "Тестовый бренд");
        Assert.AreEqual("12345", string.Concat(WebUtility.HtmlDecode(Regex.Match(cards[0].Value, "<strong>([^<]+)</strong>").Groups[1].Value).Where(char.IsDigit)));
        StringAssert.Contains(cards[0].Value, "/images/hero/9060-urban-feet-v6-960.webp");
        Assert.AreEqual(0, Regex.Matches(html, "<article class=\"placeholder-card\"").Count);
        var image = Regex.Match(cards[0].Value, "<img[^>]*src=\"([^\"]+)\"");
        Assert.IsTrue(image.Success);
        using var photo = await client.GetAsync(WebUtility.HtmlDecode(image.Groups[1].Value));
        Assert.AreEqual(HttpStatusCode.OK, photo.StatusCode);
        Assert.IsTrue(photo.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.Ordinal) == true);
        Assert.IsGreaterThan(0, (await photo.Content.ReadAsByteArrayAsync()).Length);
    }

    [TestMethod]
    public async Task Home_ImagesStylesScriptsAndFontAreAvailable()
    {
        var html = await client.GetStringAsync("/");
        var resources = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(html, "(?:src|href|data-src|data-variant-image-url)=\"([^\"]+)\""))
        {
            var path = WebUtility.HtmlDecode(match.Groups[1].Value);
            if (Regex.IsMatch(path, "^/(images|css|js|fonts)/")) resources.Add(path);
        }
        foreach (Match match in Regex.Matches(html, "(?:srcset|data-srcset|imagesrcset)=\"([^\"]+)\""))
            foreach (var candidate in WebUtility.HtmlDecode(match.Groups[1].Value).Split(','))
            {
                var path = candidate.Trim().Split(' ')[0];
                if (Regex.IsMatch(path, "^/(images|css|js|fonts)/")) resources.Add(path);
            }

        Assert.IsTrue(resources.Any(path => path.StartsWith("/images/", StringComparison.Ordinal)));
        Assert.IsTrue(resources.Any(path => path.StartsWith("/css/", StringComparison.Ordinal)));
        Assert.IsTrue(resources.Any(path => path.StartsWith("/js/", StringComparison.Ordinal)));
        foreach (var path in resources)
        {
            using var response = await client.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"Ресурс не загрузился: {path}");
            Assert.IsGreaterThan(0, (await response.Content.ReadAsByteArrayAsync()).Length, $"Ресурс пуст: {path}");
        }
    }

    [TestMethod]
    [DataRow("/Catalog")]
    [DataRow("/Catalog/Index")]
    [DataRow("/Catalog?page=2")]
    [DataRow("/Catalog?q=9060&brand=Nike")]
    public async Task Catalog_GeneralPageDoesNotExist(string path)
    {
        using var response = await client.GetAsync(path);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        catalog.Verify(service => service.GetCatalogAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Home_PromotionalLinksOpenHomeCatalog()
    {
        foreach (var path in new[] { "/", "/Home/Sort?tag=new&sort=price-asc" })
        {
            var html = await client.GetStringAsync(path);
            var allModelsLink = Regex.Match(html, "<a\\b[^>]*href=\"([^\"]+)\"[^>]*>\\s*Все модели");
            Assert.IsTrue(allModelsLink.Success);
            var destination = WebUtility.HtmlDecode(allModelsLink.Groups[1].Value);
            Assert.AreEqual("/#catalog", destination);
            var catalogLinks = Regex.Matches(html, "<a\\b[^>]*href=\"([^\"]*#catalog)\"[^>]*>");
            Assert.AreEqual(11, catalogLinks.Count, "Ссылки баннеров, фотографий, «Все модели» и «Сбросить» должны вести к каталогу.");
            foreach (Match link in catalogLinks)
                Assert.AreEqual(destination, WebUtility.HtmlDecode(link.Groups[1].Value));
            Assert.IsFalse(Regex.IsMatch(html, "href=\"/Catalog(?:[/#?\"])", RegexOptions.IgnoreCase));
            var catalogHtml = await client.GetStringAsync(destination);
            StringAssert.Contains(catalogHtml, "id=\"catalog\"");
        }
    }

    [TestMethod]
    public async Task Home_CatalogCardsRemainInactive()
    {
        var html = await client.GetStringAsync("/");

        Assert.IsFalse(Regex.IsMatch(html, "href=\"[^\"]*Catalog/Details", RegexOptions.IgnoreCase));
        Assert.AreEqual(0, Regex.Matches(html, "<article class=\"placeholder-card\"").Count);
        Assert.IsFalse(html.Contains("data-variant-choice", StringComparison.Ordinal));
        var cards = Regex.Matches(html, "<article[^>]*data-product-card[^>]*>([\\s\\S]*?)</article>");
        Assert.AreEqual(1, cards.Count);
        foreach (Match card in cards)
            Assert.IsFalse(Regex.IsMatch(card.Value, "<(?:a|button|input|select)\\b|\\bonclick\\s*=", RegexOptions.IgnoreCase));
        using var response = await client.GetAsync("/Catalog/Details/1");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task HomeAndCollections_DoNotAttemptDatabaseConnections()
    {
        using var home = await client.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.OK, home.StatusCode);
        foreach (var path in new[] { "/Catalog/Hits", "/Catalog/New" })
        {
            var html = WebUtility.HtmlDecode(await client.GetStringAsync(path));
            StringAssert.Contains(html, "Реализуется в будущем");
            Assert.AreEqual(0, Regex.Matches(html, "<article class=\"placeholder-card\"").Count);
        }
        catalog.Verify(service => service.GetCatalogAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        databaseGuard.Verify(guard => guard.ConnectionOpening(It.IsAny<DbConnection>(),
            It.IsAny<ConnectionEventData>(), It.IsAny<InterceptionResult>()), Times.Never);
        databaseGuard.Verify(guard => guard.ConnectionOpeningAsync(It.IsAny<DbConnection>(),
            It.IsAny<ConnectionEventData>(), It.IsAny<InterceptionResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [TestCategory("CatalogTiles")]
    public async Task Home_EmptyCatalogShowsMessageInsteadOfPlaceholders()
    {
        homeModel.Products.Clear();

        var html = await client.GetStringAsync("/");

        StringAssert.Contains(WebUtility.HtmlDecode(html), "В каталоге пока нет товаров.");
        Assert.AreEqual(0, Regex.Matches(html, "data-product-card").Count);
        Assert.AreEqual(0, Regex.Matches(html, "<article class=\"placeholder-card\"").Count);
    }
}
