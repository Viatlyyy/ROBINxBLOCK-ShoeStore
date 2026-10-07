using System.Globalization;
using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Models;

namespace ShoeStore.Tests.Integration;

[TestClass]
[DoNotParallelize]
public class ProductCardTests
{
    private static TemporaryPostgreSql database = null!;
    private static ProductDetailsApplicationFactory application = null!;
    private static HttpClient client = null!;
    private static string initialCatalogSnapshot = null!;

    [ClassInitialize]
    public static async Task Setup(TestContext context)
    {
        database = await TemporaryPostgreSql.StartAsync();
        try
        {
            await database.ImportSchemaAsync(Path.Combine(ProductDetailsApplicationFactory.ProjectRoot, "Database", "shoestore-schema.sql"));
            await CatalogFixture.SeedAsync(database.ConnectionString);
            application = new ProductDetailsApplicationFactory(database.ConnectionString);
            client = application.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false, HandleCookies = false
            });
            initialCatalogSnapshot = await database.CatalogSnapshotAsync();
        }
        catch
        {
            application?.Dispose();
            await database.DisposeAsync();
            throw;
        }
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        client?.Dispose();
        application?.Dispose();
        if (database is not null) await database.DisposeAsync();
    }

    [TestMethod]
    public async Task PublishedCards_OpenAnonymouslyAndShowDatabaseValues()
    {
        await using var db = CatalogFixture.Open(database.ConnectionString);
        var products = await db.Products.AsNoTracking().Include(p => p.Brand)
            .Include(p => p.Variants).ThenInclude(v => v.Sizes)
            .Where(p => p.Status == ProductStatus.Active).ToListAsync();
        CollectionAssert.AreEquivalent(CatalogFixture.Models.Select(m => m.Id).ToArray(), products.Select(p => p.Id).ToArray());
        foreach (var product in products)
        {
            using var page = await GetPageAsync($"/Catalog/Details/{product.Id}");
            var details = Required(page, ".details");
            Assert.AreEqual(product.Brand.Name, Required(details, ".eyebrow").TextContent.Trim(), $"Бренд {product.Id}");
            Assert.AreEqual(product.Name, Required(details, "h1").TextContent.Trim(), $"Название {product.Id}");
            Assert.AreEqual(product.Description, Required(details, ".description").TextContent.Trim(), $"Описание {product.Id}");
            var price = Required(details, "h2").TextContent;
            Assert.AreEqual(product.Price.ToString("0", CultureInfo.InvariantCulture), string.Concat(price.Where(char.IsDigit)), $"Цена {product.Id}");
            StringAssert.Contains(price, "₽");
            var selected = product.Variants.Single(v => v.IsDefault);
            Assert.AreEqual(selected.Name, Required(details, "[data-variant-label]").TextContent.Trim());
            Assert.AreEqual(selected.ImageUrl, ImagePath(Required(details, "[data-gallery-main]").GetAttribute("src")));
            foreach (var variant in product.Variants)
            {
                var group = Required(details, $"[data-size-options='{variant.Id}']");
                Assert.AreEqual(!variant.IsDefault, group.HasAttribute("hidden"));
                var inputs = group.QuerySelectorAll("input[name='size']");
                CollectionAssert.AreEqual(variant.Sizes.Where(s => s.StockQuantity > 0).OrderBy(s => s.Size)
                    .Select(s => s.Size.ToString(CultureInfo.InvariantCulture)).ToArray(), inputs.Select(i => i.GetAttribute("value")).ToArray());
                foreach (var input in inputs) Assert.AreEqual(!variant.IsDefault, input.HasAttribute("disabled"));
            }
        }
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/Catalog/New")]
    [DataRow("/Catalog/Hits")]
    public async Task EveryTile_LinksToItsOwnProduct(string route)
    {
        int[] expectedIds = route switch
        {
            "/Catalog/New" => [4, 7, 9, 11, 13, 15, 18, 23],
            "/Catalog/Hits" => [1, 2, 3, 8, 16, 19, 22, 25],
            _ => [1, 2, 3, 4, 7, 8, 9, 11, 13, 15, 16, 18, 19, 22, 23, 25]
        };
        using var page = await GetPageAsync(route);
        var cards = page.QuerySelectorAll("[data-product-card]");
        Assert.AreEqual(expectedIds.Length, cards.Length, route);
        var actualIds = new List<int>();
        foreach (var card in cards)
        {
            var link = Required(card, "a").GetAttribute("href")!;
            Assert.IsTrue(link.StartsWith("/Catalog/Details/", StringComparison.Ordinal), link);
            Assert.IsTrue(int.TryParse(link["/Catalog/Details/".Length..], out var id), link);
            actualIds.Add(id);
            var model = CatalogFixture.Models.Single(m => m.Id == id);
            Assert.AreEqual(model.Name, Required(card, "h3").TextContent.Trim(), link);
            Assert.AreEqual(model.Brand, Required(card, ".brand").TextContent.Trim(), link);
            using var details = await GetPageAsync(link);
            Assert.AreEqual(Required(card, "h3").TextContent.Trim(), Required(details, ".details h1").TextContent.Trim(), link);
            Assert.AreEqual(Required(card, ".brand").TextContent.Trim(), Required(details, ".details .eyebrow").TextContent.Trim(), link);
        }
        CollectionAssert.AreEquivalent(expectedIds, actualIds.ToArray(), route);
    }

    [TestMethod]
    public async Task VariantGroups_ShowTheirOwnColorsPhotosAndDefaultSelection()
    {
        await using var db = CatalogFixture.Open(database.ConnectionString);
        var variants = await db.ProductVariants.AsNoTracking().Include(v => v.GalleryImages)
            .Where(v => v.ProductId == 1).ToListAsync();
        using var page = await GetPageAsync("/Catalog/Details/1");
        Assert.AreEqual(variants.Count, page.QuerySelectorAll("[data-variant-id]").Length);
        Assert.AreEqual(variants.Count, page.QuerySelectorAll("[data-gallery-images]").Length);
        foreach (var variant in variants)
        {
            var swatch = Required(page, $"[data-variant-id='{variant.Id}']");
            Assert.AreEqual(variant.Name, swatch.GetAttribute("data-variant-name"));
            Assert.AreEqual(variant.Name, swatch.GetAttribute("aria-label"));
            Assert.AreEqual($"--swatch:{variant.Swatch}", swatch.GetAttribute("style"));
            Assert.AreEqual(variant.IsDefault ? "true" : "false", swatch.GetAttribute("aria-pressed"));
            Assert.AreEqual(variant.ImageUrl, ImagePath(swatch.GetAttribute("data-variant-image-url")));
            var gallery = Required(page, $"[data-gallery-images='{variant.Id}']");
            Assert.AreEqual(!variant.IsDefault, gallery.HasAttribute("hidden"));
            var expected = new[] { variant.ImageUrl }.Concat(variant.GalleryImages.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl)).ToArray();
            CollectionAssert.AreEqual(expected, gallery.QuerySelectorAll("[data-gallery-source]")
                .Select(i => ImagePath(i.GetAttribute("data-gallery-source"))).ToArray());
        }
        Assert.AreEqual("Black / Castlerock", Required(page, "[data-variant-label]").TextContent);
        CollectionAssert.AreEqual(new[]
        {
            "/images/gallery/optimized/colors/9060-black-castlerock-side.jpg",
            "/images/gallery/optimized/colors/9060-black-castlerock-front.jpg",
            "/images/gallery/optimized/colors/9060-black-castlerock-rear.jpg"
        }, Required(page, "[data-gallery-images='29']").QuerySelectorAll("[data-gallery-source]")
            .Select(i => ImagePath(i.GetAttribute("data-gallery-source"))).ToArray());
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task NoAvailableSizes_ShowsOutOfStock(int id)
    {
        using var page = await GetPageAsync($"/Catalog/Details/{id}");
        var group = Required(page, $"[data-size-options='{id}']");
        Assert.AreEqual(0, group.QuerySelectorAll("input[name='size']").Length);
        Assert.AreEqual("Нет в наличии", Required(group, ".size-empty").TextContent.Trim());
    }

    [TestMethod]
    [DataRow(999999)]
    [DataRow(10001)]
    [DataRow(10002)]
    public async Task MissingDraftAndArchivedCards_Return404(int id)
    {
        using var response = await client.GetAsync($"/Catalog/Details/{id}");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, $"ID {id}");
        using var page = await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync());
        Assert.IsNull(page.QuerySelector("[data-product-gallery]"));
    }

    [TestMethod]
    public async Task CardImagesStylesAndScripts_LoadSuccessfully()
    {
        var resources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in CatalogFixture.Models)
        {
            using var page = await GetPageAsync($"/Catalog/Details/{model.Id}");
            foreach (var path in new[] { "/css/product-gallery.css", "/css/quantity-picker.css", "/js/product-gallery.js", "/js/quantity-picker.js" })
                Assert.IsTrue(page.QuerySelectorAll("[src], link[rel='stylesheet']")
                    .Any(element => (element.GetAttribute("src") ?? element.GetAttribute("href"))?.StartsWith(path, StringComparison.Ordinal) == true),
                    $"В карточке {model.Id} отсутствует {path}");
            foreach (var element in page.QuerySelectorAll("[src], link[rel='stylesheet'], [data-gallery-source], [data-variant-image-url]"))
                foreach (var attribute in new[] { "src", "href", "data-gallery-source", "data-variant-image-url" })
                {
                    var url = element.GetAttribute(attribute);
                    if (url is not null && (url.StartsWith("/images/") || url.StartsWith("/css/") || url.StartsWith("/js/")))
                        resources.Add(url);
                }
        }
        Assert.IsTrue(resources.Any(url => url.StartsWith("/images/")));
        Assert.IsTrue(resources.Any(url => url.StartsWith("/css/product-gallery.css")));
        Assert.IsTrue(resources.Any(url => url.StartsWith("/js/product-gallery.js")));
        foreach (var url in resources)
        {
            using var response = await client.GetAsync(url);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, url);
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (url.StartsWith("/images/")) Assert.IsTrue(mediaType?.StartsWith("image/") == true, url);
            else if (url.StartsWith("/css/")) Assert.AreEqual("text/css", mediaType, url);
            else Assert.IsTrue(mediaType is "text/javascript" or "application/javascript", url);
            Assert.IsGreaterThan(0, (await response.Content.ReadAsByteArrayAsync()).Length, url);
        }
    }

    [TestMethod]
    public async Task OpeningCards_DoesNotChangeAnyCatalogRows()
    {
        foreach (var id in CatalogFixture.Models.Select(m => m.Id).Concat([10001, 10002, 999999]))
        {
            using var response = await client.GetAsync($"/Catalog/Details/{id}");
            Assert.AreEqual(id < 10000 ? HttpStatusCode.OK : HttpStatusCode.NotFound, response.StatusCode);
        }
        Assert.AreEqual(initialCatalogSnapshot, await database.CatalogSnapshotAsync(), "GET изменил данные каталога.");
    }

    private static async Task<IDocument> GetPageAsync(string route)
    {
        using var response = await client.GetAsync(route);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, route);
        Assert.IsNull(response.Headers.Location, route);
        Assert.AreEqual("text/html", response.Content.Headers.ContentType?.MediaType, route);
        return await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync());
    }

    private static IElement Required(IParentNode parent, string selector)
    {
        var element = parent.QuerySelector(selector);
        Assert.IsNotNull(element, $"Не найден элемент {selector}");
        return element;
    }

    private static string ImagePath(string? url)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(url), "Отсутствует URL фотографии.");
        return url.Split('?')[0];
    }
}
