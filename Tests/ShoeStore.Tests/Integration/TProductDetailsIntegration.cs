using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Integration;

[TestClass]
[TestCategory("ProductDetails")]
public class TProductDetailsIntegration
{
    private ProductCardViewModel[] products = [];
    private WebApplicationFactory<Program> application = null!;
    private HttpClient client = null!;

    [TestInitialize]
    public void Setup()
    {
        products =
        [
            new ProductCardViewModel
            {
                Id = 101, Name = "Учебная модель", BrandName = "Тестовый бренд", Description = "Описание первого товара",
                Price = 12345m, ImageUrl = "/images/products/9060.jpg",
                GalleryImageUrls = ["/images/products/9060.jpg", "/images/products/2002r.jpg"]
            },
            new ProductCardViewModel
            {
                Id = 102, Name = "Другой товар", BrandName = "Другой бренд", Description = "Описание второго товара",
                Price = 23456m, ImageUrl = "/images/products/530.jpg",
                GalleryImageUrls = ["/images/products/530.jpg", "/images/products/574-legacy.jpg"]
            }
        ];
        var catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        catalog.Setup(service => service.GetHomeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HomePageViewModel { Products = products.ToList() });
        foreach (var product in products)
            catalog.Setup(service => service.GetProduct(product.Id)).Returns(product);

        application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Host=127.0.0.1;Port=1;Database=unused_product_models;Username=unused;Timeout=1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICatalogQueryService>();
                services.AddSingleton(catalog.Object);
                var roles = new Mock<RoleManager<IdentityRole>>(
                    Mock.Of<IRoleStore<IdentityRole>>(), Array.Empty<IRoleValidator<IdentityRole>>(),
                    new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
                    NullLogger<RoleManager<IdentityRole>>.Instance);
                roles.Setup(manager => manager.RoleExistsAsync("Customer")).ReturnsAsync(true);
                services.RemoveAll<RoleManager<IdentityRole>>();
                services.AddSingleton(roles.Object);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
        client = application.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        client?.Dispose();
        application?.Dispose();
    }

    [TestMethod]
    public async Task CardClick_DisplaysSelectedProductInformation()
    {
        var homeHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        foreach (var product in products)
        {
            var link = Regex.Match(homeHtml, $"href=\"(?<url>/Catalog/Details/{product.Id})\"").Groups["url"].Value;
            Assert.IsFalse(string.IsNullOrWhiteSpace(link));
            var html = WebUtility.HtmlDecode(await client.GetStringAsync(link));

            StringAssert.Contains(html, $"<h1>{product.Name}</h1>");
            StringAssert.Contains(html, $"<p class=\"eyebrow\">{product.BrandName}</p>");
            StringAssert.Contains(html, $"<p class=\"description\">{product.Description}</p>");
            StringAssert.Contains(html.Replace("\u00a0", "").Replace("\u202f", "").Replace(" ", ""), $"<h2>{product.Price:0}₽</h2>");
            StringAssert.Contains(html, $"<img src=\"{product.GalleryImageUrls[0]}?");
            foreach (var image in product.GalleryImageUrls)
                StringAssert.Contains(html, $"data-gallery-source=\"{image}?");
        }
    }
}
