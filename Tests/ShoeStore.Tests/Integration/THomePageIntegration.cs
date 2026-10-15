using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ShoeStore.Tests.Integration;








[TestClass]
[DoNotParallelize]
public class THomePageIntegration
{
    private HomePageApplicationFactory application = null!;
    private HttpClient client = null!;

    [TestInitialize]
    public void Setup()
    {
        application = new HomePageApplicationFactory();

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
        foreach (Match card in cards)
        {
            StringAssert.Contains(card.Value, "href=\"/Catalog/Details/7001\"");
        }
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
        foreach (Match match in Regex.Matches(html, "(?:src|href|data-src)=\"([^\"]+)\""))
        {
            var path = WebUtility.HtmlDecode(match.Groups[1].Value);
            if (IsLocalResource(path)) resources.Add(path);
        }
        foreach (Match match in Regex.Matches(html, "(?:srcset|data-srcset|imagesrcset)=\"([^\"]+)\""))
            foreach (var candidate in WebUtility.HtmlDecode(match.Groups[1].Value).Split(','))
            {
                var path = candidate.Trim().Split(' ')[0];
                if (IsLocalResource(path)) resources.Add(path);
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
        Assert.AreEqual(0, application.CatalogRequestCount);
    }

    [TestMethod]
    public async Task Home_CommunityArrowOpensHits()
    {
        var html = await client.GetStringAsync("/");
        var communityLink = Regex.Match(html, "<a\\b[^>]*class=\"culture-board__community\"[^>]*>");
        Assert.IsTrue(communityLink.Success);
        var destination = Regex.Match(communityLink.Value, "href=\"([^\"]+)\"").Groups[1].Value;

        Assert.AreEqual("/Catalog/Hits", destination);
        var hits = WebUtility.HtmlDecode(await client.GetStringAsync(destination));
        StringAssert.Contains(hits, "<h1 id=\"selection-title\">Хиты</h1>");
        Assert.IsFalse(Regex.IsMatch(html, "href=\"/Catalog(?:/Index)?(?:[?\"])", RegexOptions.IgnoreCase));
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
        Assert.AreEqual(0, application.CatalogRequestCount);
        Assert.AreEqual(0, application.DatabaseConnectionAttempts);
    }

    [TestMethod]
    [TestCategory("CatalogTiles")]
    public async Task Home_EmptyCatalogShowsMessageInsteadOfPlaceholders()
    {
        application.HomeModel.Products.Clear();

        var html = await client.GetStringAsync("/");

        StringAssert.Contains(WebUtility.HtmlDecode(html), "В каталоге пока нет товаров.");
        Assert.AreEqual(0, Regex.Matches(html, "data-product-card").Count);
        Assert.AreEqual(0, Regex.Matches(html, "<article class=\"placeholder-card\"").Count);
    }

    private static bool IsLocalResource(string path) =>
        path.StartsWith("/images/", StringComparison.Ordinal)
        || path.StartsWith("/css/", StringComparison.Ordinal)
        || path.StartsWith("/js/", StringComparison.Ordinal)
        || path.StartsWith("/fonts/", StringComparison.Ordinal);
}
