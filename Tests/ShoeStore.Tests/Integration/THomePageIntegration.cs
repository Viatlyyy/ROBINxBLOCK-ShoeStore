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
                Id = 7001, IsNew = true, IsPopular = true, Name = "Модель из базы", BrandName = "Тестовый бренд", Price = 12345m,
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
    public async Task Catalog_DirectVisitDisplaysFiltersAndClickableCards(string path)
    {
        var html = await client.GetStringAsync(path);
        StringAssert.Contains(html, "id=\"catalog-filters\"");
        StringAssert.Contains(html, "action=\"/Catalog\"");
        StringAssert.Contains(html, "data-url=\"/Catalog/SearchResults\"");
        StringAssert.Contains(html, "href=\"/Catalog/Details/7001\"");
        StringAssert.Contains(html, "data-product-badge=\"new\"");
        StringAssert.Contains(html, "data-product-badge=\"popular\"");
        StringAssert.Contains(html, "/js/catalog-search.js");
        Assert.IsFalse(html.Contains("hero-carousel"));
        catalog.Verify(service => service.GetHomeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Home_ShowsOriginalGridAndLinksToCollections()
    {
        var html = await client.GetStringAsync("/");
        Assert.IsFalse(html.Contains("catalog-filters"));
        Assert.IsFalse(html.Contains("catalog-search.js"));
        Assert.IsFalse(html.Contains("data-product-badge"));
        StringAssert.Contains(html, "href=\"/Catalog/Details/7001\"");
        foreach (var region in new[] { "header", "footer" })
        {
            var regionHtml = Regex.Match(html, $"<{region}\\b[^>]*>[\\s\\S]*?</{region}>").Value;
            var nav = Regex.Match(regionHtml, "<nav\\b[^>]*>[\\s\\S]*?</nav>");
            Assert.IsTrue(nav.Success, $"Отсутствует навигация в {region}.");
            StringAssert.Contains(nav.Value, "href=\"/Catalog\"");
            Assert.IsFalse(nav.Value.Contains("/Catalog?tag=new"), "В навигации нет пункта Новинки.");
            Assert.IsFalse(nav.Value.Contains("/Catalog?tag=popular"), "В навигации нет пункта Хиты.");
        }
        var content = Regex.Match(html, "<main>([\\s\\S]*?)</main>").Value;
        Assert.AreEqual(4, Regex.Matches(content, "href=\"/Catalog\\?tag=new\"").Count);
        Assert.AreEqual(5, Regex.Matches(content, "href=\"/Catalog\\?tag=popular\"").Count);
        StringAssert.Contains(content, "href=\"/Catalog\">Все модели");
    }

    [TestMethod]
    [DataRow("/Catalog/New", "/Catalog", "new")]
    [DataRow("/Catalog/Hits", "/Catalog", "popular")]
    [DataRow("/Home/Sort?tag=new", "/Catalog", "new")]
    [DataRow("/Home/SearchResults?tag=new", "/Catalog/SearchResults", "new")]
    public async Task LegacyRoutes_KeepSelectionAndResponseType(string path, string target, string tag)
    {
        var separator = path.Contains('?') ? '&' : '?';
        using var response = await client.GetAsync($"{path}{separator}sort=price-asc&search=Dunk&brandIds=3&brandIds=7");
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        var location = new Uri(client.BaseAddress!, response.Headers.Location!);
        Assert.AreEqual(target, location.AbsolutePath);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(location.Query);
        Assert.AreEqual(tag, query["tag"].ToString());
        Assert.AreEqual("price-asc", query["sort"].ToString());
        Assert.AreEqual("Dunk", query["search"].ToString());
        CollectionAssert.AreEqual(new[] { "3", "7" }, query["brandIds"].ToArray());
        var html = await client.GetStringAsync(location);
        Assert.AreEqual(target == "/Catalog", html.Contains("<html"));
        StringAssert.Contains(html, "data-product-badge=\"new\"");
        StringAssert.Contains(html, "data-product-badge=\"popular\"");
        databaseGuard.Verify(guard => guard.ConnectionOpening(It.IsAny<DbConnection>(),
            It.IsAny<ConnectionEventData>(), It.IsAny<InterceptionResult>()), Times.Never);
        databaseGuard.Verify(guard => guard.ConnectionOpeningAsync(It.IsAny<DbConnection>(),
            It.IsAny<ConnectionEventData>(), It.IsAny<InterceptionResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Catalog_ExplainsEmptyCatalogAndEmptySelection()
    {
        homeModel.Products.Clear();
        StringAssert.Contains(WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog")), "В каталоге пока нет товаров.");
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog/SearchResults?search=Dunk"));
        StringAssert.Contains(html, "По выбранным параметрам ничего не найдено.");
        Assert.IsFalse(html.Contains("<html"));
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
