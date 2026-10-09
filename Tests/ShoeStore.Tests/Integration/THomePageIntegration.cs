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
    public async Task Home_RendersCatalogAndSixteenPlaceholders()
    {
        var html = await client.GetStringAsync("/");
        var text = WebUtility.HtmlDecode(html);

        StringAssert.Contains(html, "id=\"catalog\"");
        StringAssert.Contains(text, "Каталог");
        Assert.IsFalse(text.Contains("Рекомендации", StringComparison.Ordinal));
        StringAssert.Contains(text, "New Balance");
        StringAssert.Contains(text, "9060");
        StringAssert.Contains(text, "3XL");
        StringAssert.Contains(text, "Dunk Low");
        Assert.AreEqual(3, Regex.Matches(html, "<article class=\"hero-slide").Count);
        Assert.AreEqual(16, Regex.Matches(html, "<article class=\"placeholder-card\"").Count);
        Assert.AreEqual(0, Regex.Matches(html, "data-product-card").Count);
    }

    [TestMethod]
    public async Task Home_ImagesStylesScriptsAndFontAreAvailable()
    {
        var html = await client.GetStringAsync("/");
        var resources = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(html, "(?:src|href|data-src|data-variant-image-url)=\"([^\"]+)\""))
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
    [DataRow("/Catalog?page=2")]
    [DataRow("/Catalog?q=9060&brand=Nike")]
    public async Task Catalog_RequestsDataAndShowsPlaceholdersWithoutLiveCards(string path)
    {
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(WebUtility.HtmlDecode(html), "Каталог");
        Assert.AreEqual(16, Regex.Matches(html, "<article class=\"placeholder-card\"").Count);
        Assert.IsFalse(html.Contains("data-product-card", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("catalog-pagination", StringComparison.Ordinal));
        StringAssert.Contains(html, "href=\"/\"");
        Assert.AreEqual(1, application.CatalogRequestCount);
        Assert.AreEqual(path.Contains("page=2", StringComparison.Ordinal) ? 2 : 1, application.RequestedCatalogPage);
    }

    [TestMethod]
    public async Task Home_PlaceholdersRemainInactive()
    {
        var html = await client.GetStringAsync("/");

        Assert.IsFalse(Regex.IsMatch(html, "href=\"[^\"]*Catalog/Details", RegexOptions.IgnoreCase));
        Assert.AreEqual(16, Regex.Matches(html, "<article class=\"placeholder-card\"").Count);
        Assert.IsFalse(html.Contains("data-variant-choice", StringComparison.Ordinal));
        var placeholders = Regex.Matches(html, "<article class=\"placeholder-card\">([\\s\\S]*?)</article>");
        foreach (Match placeholder in placeholders)
            Assert.IsFalse(Regex.IsMatch(placeholder.Value, "<(?:a|button)\\b", RegexOptions.IgnoreCase));
        using var response = await client.GetAsync("/Catalog/Details/1");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task HomeAndCatalog_DoNotAttemptDatabaseConnections()
    {
        using var home = await client.GetAsync("/");
        using var catalog = await client.GetAsync("/Catalog");

        Assert.AreEqual(HttpStatusCode.OK, home.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, catalog.StatusCode);
        foreach (var path in new[] { "/Catalog/Hits", "/Catalog/New" })
        {
            var html = WebUtility.HtmlDecode(await client.GetStringAsync(path));
            StringAssert.Contains(html, "Реализуется в будущем");
            Assert.AreEqual(0, Regex.Matches(html, "<article class=\"placeholder-card\"").Count);
        }
        Assert.AreEqual(1, application.CatalogRequestCount);
        Assert.AreEqual(0, application.DatabaseConnectionAttempts);
    }

    private static bool IsLocalResource(string path) =>
        path.StartsWith("/images/", StringComparison.Ordinal)
        || path.StartsWith("/css/", StringComparison.Ordinal)
        || path.StartsWith("/js/", StringComparison.Ordinal)
        || path.StartsWith("/fonts/", StringComparison.Ordinal);
}
