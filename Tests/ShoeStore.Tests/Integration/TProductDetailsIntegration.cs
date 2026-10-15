using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Tests.Integration;

[TestClass]
[DoNotParallelize]
[TestCategory("ProductDetails")]
public class TProductDetailsIntegration
{
    private DbContextOptions<ApplicationDbContext> options = null!;
    private WebApplicationFactory<Program> application = null!;
    private HttpClient client = null!;

    [TestInitialize]
    public async Task Setup()
    {
        var settings = new ConfigurationBuilder().AddUserSecrets<Program>().AddEnvironmentVariables().Build();
        var server = settings.GetConnectionString("DefaultConnection");
        Assert.IsFalse(string.IsNullOrWhiteSpace(server), "Настройте ConnectionStrings:DefaultConnection в секретах проекта.");
        var connection = new NpgsqlConnectionStringBuilder(server)
        {
            Database = "shoestore_product_tests_" + Guid.NewGuid().ToString("N"),
            Pooling = false
        };
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var brand = new Brand { Id = 201, Name = "Тестовый бренд" };
        var otherBrand = new Brand { Id = 202, Name = "Другой бренд" };
        var category = new Category { Id = 201, Name = "Кроссовки" };
        db.Products.AddRange(
            new Product
            {
                Id = 101, Name = "Учебная модель", Description = "Описание из PostgreSQL", Price = 12345m,
                ImageUrl = "/images/products/9060.jpg", Status = ProductStatus.Active, Brand = brand, Category = category,
                Variants =
                [
                    new ProductVariant
                    {
                        Id = 301, Name = "Основной", Sku = "TEST-101", IsDefault = true,
                        ImageUrl = "/images/products/9060.jpg",
                        Sizes = [new ProductVariantSize { Size = 38, StockQuantity = 5 }, new ProductVariantSize { Size = 39, StockQuantity = 0 }],
                        GalleryImages = [new ProductVariantImage { ImageUrl = "/images/products/2002r.jpg", SortOrder = 1 }]
                    }
                ]
            },
            new Product
            {
                Id = 102, Name = "Другой товар", Description = "Другое описание", Price = 23456m,
                ImageUrl = "/images/products/530.jpg", Status = ProductStatus.Active, Brand = otherBrand, Category = category,
                Variants =
                [
                    new ProductVariant
                    {
                        Id = 303, Name = "Основной", Sku = "TEST-102", IsDefault = true,
                        ImageUrl = "/images/products/530.jpg",
                        Sizes = [new ProductVariantSize { Size = 41, StockQuantity = 3 }],
                        GalleryImages = [new ProductVariantImage { ImageUrl = "/images/products/574-legacy.jpg", SortOrder = 1 }]
                    }
                ]
            },
            new Product { Id = 103, Name = "Черновик", Price = 100m, Status = ProductStatus.Draft, Brand = brand, Category = category },
            new Product { Id = 104, Name = "Архив", Price = 100m, Status = ProductStatus.Archived, Brand = brand, Category = category });
        await db.SaveChangesAsync();

        application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString);
            builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        });
        client = application.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        client?.Dispose();
        if (application is not null) await application.DisposeAsync();
        if (options is not null)
        {
            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

    [TestMethod]
    public async Task Home_LinksToProductDetails()
    {
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        StringAssert.Contains(html, "href=\"/Catalog/Details/101\"");
        StringAssert.Contains(html, "href=\"/Catalog/Details/102\"");
        Assert.IsFalse(html.Contains("href=\"/Catalog/Details/103\""));
        Assert.IsFalse(html.Contains("href=\"/Catalog/Details/104\""));
    }

    [TestMethod]
    public async Task Details_RendersProductInformationFromPostgreSql()
    {
        foreach (var expected in new[]
        {
            (Id: 101, Name: "Учебная модель", Brand: "Тестовый бренд", Description: "Описание из PostgreSQL",
                Price: "12345", Image: "9060.jpg", Gallery: "2002r.jpg", Size: "38"),
            (Id: 102, Name: "Другой товар", Brand: "Другой бренд", Description: "Другое описание",
                Price: "23456", Image: "530.jpg", Gallery: "574-legacy.jpg", Size: "41")
        })
        {
            using var response = await client.GetAsync($"/Catalog/Details/{expected.Id}");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual("text/html", response.Content.Headers.ContentType?.MediaType);
            Assert.IsNull(response.Headers.Location);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

            StringAssert.Contains(html, $"<h1>{expected.Name}</h1>");
            StringAssert.Contains(html, $"<p class=\"eyebrow\">{expected.Brand}</p>");
            StringAssert.Contains(html, $"<p class=\"description\">{expected.Description}</p>");
            StringAssert.Contains(html.Replace("\u00a0", "").Replace("\u202f", "").Replace(" ", ""), $"<h2>{expected.Price}₽</h2>");
            StringAssert.Contains(html, $"<img src=\"/images/products/{expected.Image}?");
            StringAssert.Contains(html, $"data-gallery-source=\"/images/products/{expected.Gallery}?");
            StringAssert.Contains(html, $"name=\"size\" value=\"{expected.Size}\"");
            Assert.IsFalse(html.Contains("name=\"size\" value=\"39\""));
            Assert.IsFalse(html.Contains(expected.Id == 101 ? "Другой товар" : "Учебная модель"));
        }
    }

    [TestMethod]
    public async Task Details_Returns404ForDraftArchivedMissingAndInvalidIds()
    {
        foreach (var id in new[] { 103, 104, 999, 0, -1 })
        {
            using var response = await client.GetAsync($"/Catalog/Details/{id}");
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, $"Id={id}");
        }
    }

}
